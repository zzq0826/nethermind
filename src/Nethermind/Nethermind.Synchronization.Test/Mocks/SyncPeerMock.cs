//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Stats.Model;

namespace Nethermind.Synchronization.Test.Mocks;

public class SyncPeerMock : ISyncPeer
{
    private readonly bool _causeTimeoutOnInit;
    private readonly bool _causeTimeoutOnBlocks;
    private readonly bool _causeTimeoutOnHeaders;
    private List<Block> Blocks { get; } = new();

    public Block HeadBlock => Blocks.Last();

    public BlockHeader HeadHeader => HeadBlock.Header;

    private static readonly Block _genesisBlock = Build.A.Block.Genesis.WithDifficulty(100000)
        .WithTotalDifficulty((UInt256)100000).TestObject;

    public SyncPeerMock(string peerName, bool causeTimeoutOnInit = false, bool causeTimeoutOnBlocks = false,
        bool causeTimeoutOnHeaders = false)
    {
        _causeTimeoutOnInit = causeTimeoutOnInit;
        _causeTimeoutOnBlocks = causeTimeoutOnBlocks;
        _causeTimeoutOnHeaders = causeTimeoutOnHeaders;
        Blocks.Add(_genesisBlock);
        UpdateHead();
        ClientId = peerName;
    }

    private void UpdateHead()
    {
        HeadHash = HeadBlock.Hash;
        HeadNumber = HeadBlock.Number;
        TotalDifficulty = HeadBlock.TotalDifficulty ?? 0;
    }

    public Node Node { get; } = new Node(Build.A.PrivateKey.TestObject.PublicKey, "127.0.0.1", 1234);

    public string ClientId { get; }
    public Keccak HeadHash { get; set; }
    public long HeadNumber { get; set; }
    public UInt256 TotalDifficulty { get; set; }

    public bool IsInitialized { get; set; }

    public void Disconnect(DisconnectReason reason, string details)
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public Task<BlockBody[]> GetBlockBodies(IReadOnlyList<Keccak> blockHashes, CancellationToken token)
    {
        if (_causeTimeoutOnBlocks)
        {
            return Task.FromException<BlockBody[]>(new TimeoutException());
        }

        BlockBody[] result = new BlockBody[blockHashes.Count];
        for (int i = 0; i < blockHashes.Count; i++)
        {
            foreach (Block block in Blocks)
            {
                if (block.Hash == blockHashes[i])
                {
                    result[i] = new BlockBody(block.Transactions, block.Uncles);
                }
            }
        }

        return Task.FromResult(result);
    }

    public Task<BlockHeader[]> GetBlockHeaders(long number, int maxBlocks, int skip, CancellationToken token)
    {
        if (_causeTimeoutOnHeaders)
        {
            return Task.FromException<BlockHeader[]>(new TimeoutException());
        }

        int filled = 0;
        bool started = false;
        BlockHeader[] result = new BlockHeader[maxBlocks];
        foreach (Block block in Blocks)
        {
            if (block.Number == number)
            {
                started = true;
            }

            if (started)
            {
                result[filled++] = block.Header;
            }

            if (filled >= maxBlocks)
            {
                break;
            }
        }

        return Task.FromResult(result);
    }

    public async Task<BlockHeader> GetHeadBlockHeader(Keccak hash, CancellationToken token)
    {
        if (_causeTimeoutOnInit)
        {
            Console.WriteLine("RESPONDING TO GET HEAD BLOCK HEADER WITH EXCEPTION");
            await Task.FromException<BlockHeader>(new TimeoutException());
        }

        BlockHeader header;
        try
        {
            header = Blocks.Last().Header;
        }
        catch (Exception)
        {
            Console.WriteLine("RESPONDING TO GET HEAD BLOCK HEADER EXCEPTION");
            throw;
        }

        Console.WriteLine($"RESPONDING TO GET HEAD BLOCK HEADER WITH RESULT {header.Number}");
        return header;
    }

    public void NotifyOfNewBlock(Block block, SendBlockPriority priority)
    {
        if (priority == SendBlockPriority.High)
            ReceivedBlocks.Push(block);
    }

    public ConcurrentStack<Block> ReceivedBlocks { get; } = new();

    public event EventHandler Disconnected;

    public PublicKey Id => Node.Id;

    public void SendNewTransactions(IEnumerable<Transaction> txs, bool sendFullTx) { }

    public Task<TxReceipt[][]> GetReceipts(IReadOnlyList<Keccak> blockHash, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task<byte[][]> GetNodeData(IReadOnlyList<Keccak> hashes, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public void AddBlocksUpTo(int i, int branchStart = 0, byte branchIndex = 0)
    {
        Block block = Blocks.Last();
        for (long j = block.Number; j < i; j++)
        {
            block = Build.A.Block.WithDifficulty(1000000).WithParent(block)
                .WithTotalDifficulty(block.TotalDifficulty + 1000000)
                .WithExtraData(j < branchStart ? Array.Empty<byte>() : new[] {branchIndex}).TestObject;
            Blocks.Add(block);
        }

        UpdateHead();
    }

    public void AddHighDifficultyBlocksUpTo(int i, int branchStart = 0, byte branchIndex = 0)
    {
        Block block = Blocks.Last();
        for (long j = block.Number; j < i; j++)
        {
            block = Build.A.Block.WithParent(block).WithDifficulty(2000000)
                .WithTotalDifficulty(block.TotalDifficulty + 2000000)
                .WithExtraData(j < branchStart ? Array.Empty<byte>() : new[] {branchIndex}).TestObject;
            Blocks.Add(block);
        }

        UpdateHead();
    }

    public void RegisterSatelliteProtocol<T>(string protocol, T protocolHandler) where T : class
    {
        throw new NotImplementedException();
    }

    public bool TryGetSatelliteProtocol<T>(string protocol, out T protocolHandler) where T : class
    {
        throw new NotImplementedException();
    }
}
