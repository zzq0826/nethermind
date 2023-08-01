// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Nethermind.Core.Collections;

public class DArray
{
    public BitVector Data { get; }
    public DArrayIndex IndexS1 { get; }
    public DArrayIndex IndexS0 { get; }

    public DArray(BitVector bv)
    {
        Data = new BitVector(bv);
        IndexS1 = new DArrayIndex(bv, true);
        IndexS0 = new DArrayIndex(bv, false);
    }

    public static DArray FromBits(IEnumerable<bool> bits)
    {
        BitVector data = new ();
        foreach (bool bit in bits) data.PushBit(bit);
        return new DArray(data);
    }

    public int? Select0(int k)
    {
        return IndexS0.Select(Data, k);
    }

    public int? Select1(int k)
    {
        return IndexS1.Select(Data, k);
    }
}

public class DArrayIndex
{
    const int BlockLen = 1024;
    const int MaxInBlockDistance = 1 << 16;
    const int SubBlockLen = 32;

    public readonly List<int> CurBlockPositions;
    public readonly List<int> BlockInventory;
    public readonly List<ushort> SubBlockInventory;
    public readonly List<int> OverflowPositions;
    public int NumPositions;
    public bool OverOne;

    public DArrayIndex(BitVector bv, bool overOne)
    {
        OverOne = overOne;
        CurBlockPositions = new List<int>();
        BlockInventory = new List<int>();
        SubBlockInventory = new List<ushort>();
        OverflowPositions = new List<int>();
        NumPositions = 0;

        for (int wordIndex = 0; wordIndex < bv.Words.Count; wordIndex++)
        {
            int currPos = wordIndex * 64;
            UIntPtr currWord = overOne ? GetWordOverOne(bv, wordIndex) : GetWordOverZero(bv, wordIndex);

            while (true)
            {
                int l = NumberOfTrailingZeros(currWord);
                currPos += l;
                currWord >>= l;
                if (currPos >= bv.Length) break;

                CurBlockPositions.Add(currPos);
                if (CurBlockPositions.Count == BlockLen)
                {
                    FlushCurBlock(CurBlockPositions, BlockInventory, SubBlockInventory, OverflowPositions);
                }

                currWord >>= 1;
                currPos += 1;
                NumPositions += 1;
            }
        }

        if (CurBlockPositions.Count > 0)
            FlushCurBlock(CurBlockPositions, BlockInventory, SubBlockInventory, OverflowPositions);
    }

    public int? Select(BitVector bv, int k)
    {
        if (NumPositions <= k) return null;

        int block = k / BlockLen;
        int blockPos = BlockInventory[block];

        if (blockPos < 0)
        {
            int overflowPos = -blockPos - 1;
            return OverflowPositions[overflowPos + (k % BlockLen)];
        }

        int subBlock = k / SubBlockLen;
        int remainder = k % SubBlockLen;

        int startPos = blockPos + SubBlockInventory[subBlock];

        int sel;
        if (remainder == 0)
        {
            sel = startPos;
        }
        else
        {
            int wordIdx = startPos / 64;
            int wordShift = startPos % 64;
            UIntPtr wordE = OverOne ? GetWordOverOne(bv, wordIdx) : GetWordOverZero(bv, wordIdx);
            UIntPtr word = wordE & (UIntPtr.MaxValue << wordShift);

            while (true)
            {
                int popCount = BitOperations.PopCount(word);
                if (remainder < popCount) break;
                remainder -= popCount;
                wordIdx += 1;
                word = OverOne ? GetWordOverOne(bv, wordIdx) : GetWordOverZero(bv, wordIdx);
            }

            sel = 64 * wordIdx + SelectInWord(word, remainder)!.Value;
        }

        return sel;
    }

    private static void FlushCurBlock(
        List<int> curBlockPositions,
        ICollection<int> blockInventory,
        ICollection<ushort> subBlockInventory,
        List<int> overflowPositions
    )
    {
        int first = curBlockPositions[0];
        int last = curBlockPositions[^1];

        if (last - first < MaxInBlockDistance)
        {
            blockInventory.Add(first);
            for (int i = 0; i < curBlockPositions.Count; i += SubBlockLen)
            {
                subBlockInventory.Add((ushort)(curBlockPositions[i] - first));
            }
        }
        else
        {
            blockInventory.Add(-(overflowPositions.Count + 1));
            overflowPositions.AddRange(curBlockPositions);

            for (int i = 0; i < curBlockPositions.Count; i += SubBlockLen)
            {
                subBlockInventory.Add(ushort.MaxValue);
            }
        }
        curBlockPositions.Clear();
    }

    private static UIntPtr GetWordOverZero(BitVector bv, int wordIdx) => ~bv.Words[wordIdx];

    private static UIntPtr GetWordOverOne(BitVector bv, int wordIdx) => bv.Words[wordIdx];

    private static int NumberOfTrailingZeros(UIntPtr i) => BitOperations.TrailingZeroCount(i);

    private static int? SelectInWord(UIntPtr x, int k)
    {
        int index = 0;
        while (x != 0)
        {
            if ((x & 1) != 0) k--;
            if (k == -1) return index;
            index++;
            x >>= 1;
        }
        return -1;
    }
}
