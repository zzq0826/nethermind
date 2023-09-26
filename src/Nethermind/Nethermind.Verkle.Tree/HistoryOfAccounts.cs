// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using Nethermind.Core.Collections.EliasFano;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Serialization.Rlp;
using Nethermind.Serialization.Rlp.EliasFano;

namespace Nethermind.Verkle.Tree;

public class HistoryOfAccounts
{
    private const int BlocksChunks = 2000;
    private readonly IDb _historyOfAccounts;
    private static readonly EliasFanoDecoder _decoder = new EliasFanoDecoder();

    public HistoryOfAccounts(IDb historyOfAccounts)
    {
        _historyOfAccounts = historyOfAccounts;
    }

    public void AppendHistoryBlockNumberForKey(Pedersen key, ulong blockNumber)
    {
        List<List<ulong>> shardsList = new List<List<ulong>>();
        List<ulong> shard = GetLastShardOfBlocks(key);
        shardsList.Add(shard);
        if(shard.Count == BlocksChunks) shardsList.Add(new List<ulong>());
        shardsList[^1].Add(blockNumber);

        InsertShards(key, shardsList);
    }

    private void InsertShards(Pedersen key, List<List<ulong>> shardsList)
    {
        foreach (var shard in shardsList)
        {
            EliasFanoBuilder efb = new(shard[^1], shard.Count);
            efb.Extend(shard);
            EliasFano ef = efb.Build();
            RlpStream streamNew = new (_decoder.GetLength(ef, RlpBehaviors.None));
            _decoder.Encode(streamNew, ef);
            HistoryKey historyKey = shard.Count == BlocksChunks
                ? new HistoryKey(key, shard[^1])
                : new HistoryKey(key, ulong.MaxValue);
            _historyOfAccounts[historyKey.Encode()] = streamNew.Data;
        }
    }

    private List<ulong> GetLastShardOfBlocks(Pedersen key)
    {
        byte[]? ef = _historyOfAccounts[(new HistoryKey(key, ulong.MaxValue)).Encode()];
        List<ulong> shard = new();
        if (ef is not null)
        {
            EliasFano eliasFanoS = _decoder.Decode(new RlpStream(ef));
            EliasFanoIterator iter = new (eliasFanoS, 0);
            while (iter.MoveNext()) shard.Add(iter.Current);
        }
        return shard;
    }
}

public readonly struct HistoryKey
{
    public Pedersen Key { get; }
    public ulong MaxBlock { get; }

    public HistoryKey(Pedersen address, ulong maxBlock)
    {
        Key = address;
        MaxBlock = maxBlock;
    }

    public byte[] Encode()
    {
        byte[] data = new byte[40];
        Span<byte> dataSpan = data;
        Key.Bytes.CopyTo(dataSpan);
        BinaryPrimitives.WriteUInt64LittleEndian(dataSpan.Slice(32), MaxBlock);
        return data;
    }
}


