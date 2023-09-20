// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Nethermind.Core.Collections.EliasFano;

public unsafe struct BitVector
{
    private static readonly int WordLen = sizeof(UIntPtr) * 8;
    public List<UIntPtr> Words;
    public int Length { get; private set; }

    public int SerializedBytesLength
    {
        get => MemoryMarshal.Cast<UIntPtr, byte>(CollectionsMarshal.AsSpan(Words)).Length;
    }

    public byte[] Serialize()
    {
        return MemoryMarshal.Cast<UIntPtr, byte>(CollectionsMarshal.AsSpan(Words)).ToArray();
    }


    public BitVector()
    {
        Words = new List<UIntPtr>();
        Length = 0;
    }

    public BitVector(int capacity)
    {
        Words = new List<UIntPtr>(new UIntPtr[WordsFor(capacity)]);
        Length = capacity;
    }

    public BitVector(BitVector bv)
    {
        Words = new List<UIntPtr>(bv.Words);
        Length = bv.Length;
    }

    private BitVector(List<UIntPtr> words, int length)
    {
        Words = words;
        Length = length;
    }

    public static BitVector WithCapacity(int length)
    {
        return new BitVector(new List<UIntPtr>(WordsFor(length)), 0);
    }

    public static BitVector FromBit(bool bit, int length)
    {
        UIntPtr word = bit ? UIntPtr.MaxValue : 0;
        List<UIntPtr> words = new List<UIntPtr>(new UIntPtr[WordsFor(length)]);
        for (int i = 0; i < words.Count; i++)
        {
            words[i] = word;
        }

        int shift = length % WordLen;
        if (shift != 0)
        {
            UIntPtr mask = ((UIntPtr)1 << shift) - 1;
            words[^1] &= mask;
        }

        return new BitVector { Words = words, Length = length };
    }

    public static BitVector FromBits(IEnumerable<bool> bits)
    {
        BitVector dArray = new BitVector();
        foreach (bool bit in bits) dArray.PushBit(bit);
        return dArray;
    }

    public void PushBit(bool bit)
    {
        int posInWord = Length % WordLen;
        if(posInWord == 0) Words.Add(Convert.ToUInt32(bit));
        else Words[^1] |= (Convert.ToUInt32(bit)) << posInWord;
        Length += 1;
    }

    public bool? GetBit(int pos)
    {
        if (pos < Length)
        {
            int block = pos / WordLen;
            int shift = pos % WordLen;
            return ((Words[block] >> shift) & 1) == 1;
        }

        return null;
    }

    public void SetBit(int pos, bool bit)
    {
        if (Length <= pos) throw new ArgumentException();

        int word = pos / WordLen;
        int posInWord = pos % WordLen;
        Words[word] &= ~((UIntPtr)1 << posInWord);
        Words[word] |= (UIntPtr)Convert.ToUInt32(bit) << posInWord;
    }

    public readonly UIntPtr? GetBits(int pos, int len)
    {
        if (WordLen < len || Length < pos + len) return null;

        if (len == 0) return 0;

        (int block, int shift) = (pos / WordLen, pos % WordLen);

        UIntPtr mask = len < WordLen ? ((UIntPtr)1 << len) - 1 : UIntPtr.MaxValue;

        UIntPtr bits = shift + len <= WordLen
            ? Words[block] >> shift & mask
            : (Words[block] >> shift) | (Words[block + 1] << (WordLen - shift) & mask);
        return bits;
    }

    public void SetBits(int pos, UIntPtr bits, int len)
    {
        if (WordLen < len || Length < pos + len) throw new ArgumentException();

        if (len == 0) return;

        UIntPtr mask = len < WordLen ? ((UIntPtr)1 << len) - 1 : UIntPtr.MaxValue;

        bits &= mask;

        int word = pos / WordLen;
        int posInWord = pos % WordLen;

        Words[word] &= ~(mask << posInWord);
        Words[word] |= bits << posInWord;

        int stored = WordLen - posInWord;

        if (stored < len)
        {
            Words[word + 1] &= ~(mask >> stored);
            Words[word + 1] |= bits >> stored;
        }
    }

    public void PushBits(UIntPtr bits, int len)
    {
        if (WordLen < len) throw new ArgumentException();
        if (len == 0) return;

        UIntPtr mask = len < WordLen ? ((UIntPtr)1 << len) - 1 : UIntPtr.MaxValue;

        bits &= mask;

        int posInWord = Length % WordLen;
        if (posInWord == 0)
        {
            Words.Add(bits);
        }
        else
        {
            Words[^1] |= bits << posInWord;
            if (len > WordLen - posInWord)
            {
                Words.Add(bits >> WordLen - posInWord);
            }
        }

        Length += len;
    }

    private static int WordsFor(int n) => (n + (WordLen - 1)) / WordLen;
}
