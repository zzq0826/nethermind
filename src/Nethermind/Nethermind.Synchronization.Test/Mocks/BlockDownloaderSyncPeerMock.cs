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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Network;
using Nethermind.Network.P2P.Subprotocols.Eth.V62.Messages;
using Nethermind.Network.P2P.Subprotocols.Eth.V63.Messages;
using Nethermind.Specs;
using Nethermind.Stats.Model;
using Nethermind.Synchronization.Blocks;

namespace Nethermind.Synchronization.Test.Mocks;

public class BlockDownloaderSyncPeerMock : ISyncPeer
{
    private readonly bool _withReceipts;
    private readonly BlockHeadersMessageSerializer _headersSerializer = new();
    private readonly BlockBodiesMessageSerializer _bodiesSerializer = new();
    private readonly ReceiptsMessageSerializer _receiptsSerializer = new(RopstenSpecProvider.Instance);

    private IDb _blockInfoDb = new MemDb();
    public IBlockTree BlockTree { get; private set; }
    private IReceiptStorage _receiptStorage = new InMemoryReceiptStorage();

    public SyncResponse Flags { get; set; }

    public BlockDownloaderSyncPeerMock(long chainLength, bool withReceipts, SyncResponse flags)
    {
        _withReceipts = withReceipts;
        Flags = flags;
        BuildTree(chainLength, withReceipts);
    }

    public BlockDownloaderSyncPeerMock(IBlockTree blockTree, bool withReceipts, SyncResponse flags)
    {
        _withReceipts = withReceipts;
        Flags = flags;
        BlockTree = blockTree;
        UpdateTree();
    }

    private void UpdateTree()
    {
        HeadNumber = BlockTree.Head.Number;
        HeadHash = BlockTree.HeadHash;
        TotalDifficulty = BlockTree.Head.TotalDifficulty ?? 0;
    }

    private void BuildTree(long chainLength, bool withReceipts)
    {
        _receiptStorage = new InMemoryReceiptStorage();
        BlockTreeBuilder builder = Build.A.BlockTree();
        if (withReceipts)
        {
            builder = builder.WithTransactions(_receiptStorage, MainnetSpecProvider.Instance);
        }

        builder = builder.OfChainLength((int)chainLength);
        BlockTree = builder.TestObject;

        UpdateTree();
    }

    public void ExtendTree(long newLength)
    {
        BuildTree(newLength, _withReceipts);
    }

    public Node Node { get; }
    public string ClientId { get; }
    public Keccak HeadHash { get; set; }
    public long HeadNumber { get; set; }
    public UInt256 TotalDifficulty { get; set; }
    public bool IsInitialized { get; set; }

    public void Disconnect(DisconnectReason reason, string details)
    {
        throw new NotImplementedException();
    }

    public async Task<BlockBody[]> GetBlockBodies(IReadOnlyList<Keccak> blockHashes, CancellationToken token)
    {
        bool consistent = Flags.HasFlag(SyncResponse.Consistent);
        bool justFirst = Flags.HasFlag(SyncResponse.JustFirst);
        bool allKnown = Flags.HasFlag(SyncResponse.AllKnown);
        bool noBody = Flags.HasFlag(SyncResponse.NoBody);

        BlockBody[] headers = new BlockBody[blockHashes.Count];
        int i = 0;
        foreach (Keccak blockHash in blockHashes)
        {
            headers[i++] = BlockTree.FindBlock(blockHash, BlockTreeLookupOptions.None).Body;
        }

        BlockBodiesMessage message = new(headers);
        byte[] messageSerialized = _bodiesSerializer.Serialize(message);
        return await Task.FromResult(_bodiesSerializer.Deserialize(messageSerialized).Bodies);
    }

    public Task<BlockHeader[]> GetBlockHeaders(Keccak blockHash, int maxBlocks, int skip,
        CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public async Task<BlockHeader[]> GetBlockHeaders(long number, int maxBlocks, int skip,
        CancellationToken token)
    {
        bool consistent = Flags.HasFlag(SyncResponse.Consistent);
        bool justFirst = Flags.HasFlag(SyncResponse.JustFirst);
        bool allKnown = Flags.HasFlag(SyncResponse.AllKnown);
        bool timeoutOnFullBatch = Flags.HasFlag(SyncResponse.TimeoutOnFullBatch);
        bool noBody = Flags.HasFlag(SyncResponse.NoBody);

        if (timeoutOnFullBatch && number == SyncBatchSize.Max)
        {
            throw new TimeoutException();
        }

        BlockHeader[] headers = new BlockHeader[maxBlocks];
        for (int i = 0; i < (justFirst ? 1 : maxBlocks); i++)
        {
            headers[i] = BlockTree.FindHeader(number + i, BlockTreeLookupOptions.None);
        }

        BlockHeadersMessage message = new(headers);
        byte[] messageSerialized = _headersSerializer.Serialize(message);
        return await Task.FromResult(_headersSerializer.Deserialize(messageSerialized).BlockHeaders);
    }

    public Task<BlockHeader> GetHeadBlockHeader(Keccak hash, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public void NotifyOfNewBlock(Block block, SendBlockPriority priority)
    {
        throw new NotImplementedException();
    }

    public PublicKey Id => Node.Id;

    public void SendNewTransactions(IEnumerable<Transaction> txs, bool sendFullTx)
    {
        throw new NotImplementedException();
    }

    public async Task<TxReceipt[][]> GetReceipts(IReadOnlyList<Keccak> blockHash, CancellationToken token)
    {
        TxReceipt[][] receipts = new TxReceipt[blockHash.Count][];
        int i = 0;
        foreach (Keccak keccak in blockHash)
        {
            Block block = BlockTree.FindBlock(keccak, BlockTreeLookupOptions.None);
            TxReceipt[] blockReceipts = _receiptStorage.Get(block);
            receipts[i++] = blockReceipts;
        }

        ReceiptsMessage message = new(receipts);
        byte[] messageSerialized = _receiptsSerializer.Serialize(message);
        return await Task.FromResult(_receiptsSerializer.Deserialize(messageSerialized).TxReceipts);
    }

    public Task<byte[][]> GetNodeData(IReadOnlyList<Keccak> hashes, CancellationToken token)
    {
        throw new NotImplementedException();
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
