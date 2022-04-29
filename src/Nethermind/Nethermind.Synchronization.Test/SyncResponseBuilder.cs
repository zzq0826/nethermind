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
using System.Linq;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Network;
using Nethermind.Network.P2P.Subprotocols.Eth.V62.Messages;
using Nethermind.Network.P2P.Subprotocols.Eth.V63.Messages;
using Nethermind.Specs;
using Nethermind.State.Proofs;
using Nethermind.Synchronization.Blocks;

namespace Nethermind.Synchronization.Test;

public class SyncResponseBuilder
{
    private IBlockTree _blockTree;
    private readonly Dictionary<long, Keccak> _testHeaderMapping;

    public SyncResponseBuilder(IBlockTree blockTree, Dictionary<long, Keccak> testHeaderMapping)
    {
        _blockTree = blockTree;
        _testHeaderMapping = testHeaderMapping;
    }

    public async Task<BlockHeader[]> BuildHeaderResponse(long startNumber, int number, SyncResponse flags)
    {
        bool consistent = flags.HasFlag(SyncResponse.Consistent);
        bool justFirst = flags.HasFlag(SyncResponse.JustFirst);
        bool allKnown = flags.HasFlag(SyncResponse.AllKnown);
        bool timeoutOnFullBatch = flags.HasFlag(SyncResponse.TimeoutOnFullBatch);
        bool noBody = flags.HasFlag(SyncResponse.NoBody);

        if (timeoutOnFullBatch && number == SyncBatchSize.Max)
        {
            throw new TimeoutException();
        }

        BlockHeader startBlock = _blockTree.FindHeader(_testHeaderMapping[startNumber], BlockTreeLookupOptions.None);
        BlockHeader[] headers = new BlockHeader[number];
        headers[0] = startBlock;
        if (!justFirst)
        {
            for (int i = 1; i < number; i++)
            {
                Keccak receiptRoot = i == 1
                    ? Keccak.EmptyTreeHash
                    : new Keccak("0x9904791428367d3f36f2be68daf170039dd0b3d6b23da00697de816a05fb5cc1");
                headers[i] = consistent
                    ? Build.A.BlockHeader.WithReceiptsRoot(receiptRoot).WithParent(headers[i - 1])
                        .WithUnclesHash(noBody ? Keccak.OfAnEmptySequenceRlp : Keccak.Zero).TestObject
                    : Build.A.BlockHeader.WithReceiptsRoot(receiptRoot).WithNumber(headers[i - 1].Number + 1)
                        .TestObject;

                if (allKnown)
                {
                    _blockTree.SuggestHeader(headers[i]);
                }

                _testHeaderMapping[startNumber + i] = headers[i].Hash;
            }
        }

        foreach (BlockHeader header in headers)
        {
            _headers[header.Hash] = header;
        }

        BlockHeadersMessage message = new(headers);
        byte[] messageSerialized = _headersSerializer.Serialize(message);
        return await Task.FromResult(_headersSerializer.Deserialize(messageSerialized).BlockHeaders);
    }

    private readonly BlockHeadersMessageSerializer _headersSerializer = new();
    private readonly BlockBodiesMessageSerializer _bodiesSerializer = new();
    private readonly ReceiptsMessageSerializer _receiptsSerializer = new(RopstenSpecProvider.Instance);

    private Dictionary<Keccak, BlockHeader> _headers = new();
    private Dictionary<Keccak, BlockBody> _bodies = new();

    public async Task<BlockBody[]> BuildBlocksResponse(IList<Keccak> blockHashes, SyncResponse flags)
    {
        bool consistent = flags.HasFlag(SyncResponse.Consistent);
        bool justFirst = flags.HasFlag(SyncResponse.JustFirst);
        bool allKnown = flags.HasFlag(SyncResponse.AllKnown);
        bool timeoutOnFullBatch = flags.HasFlag(SyncResponse.TimeoutOnFullBatch);
        bool withTransactions = flags.HasFlag(SyncResponse.WithTransactions);

        if (timeoutOnFullBatch && blockHashes.Count == SyncBatchSize.Max)
        {
            throw new TimeoutException();
        }

        BlockHeader startHeader = _blockTree.FindHeader(blockHashes[0], BlockTreeLookupOptions.None);
        if (startHeader == null) startHeader = _headers[blockHashes[0]];

        BlockHeader[] blockHeaders = new BlockHeader[blockHashes.Count];
        BlockBody[] blockBodies = new BlockBody[blockHashes.Count];
        blockBodies[0] = new BlockBody(new Transaction[0], new BlockHeader[0]);
        blockHeaders[0] = startHeader;

        _bodies[startHeader.Hash] = blockBodies[0];
        _headers[startHeader.Hash] = blockHeaders[0];
        if (!justFirst)
        {
            for (int i = 0; i < blockHashes.Count; i++)
            {
                blockHeaders[i] = consistent
                    ? _headers[blockHashes[i]]
                    : Build.A.BlockHeader.WithNumber(blockHeaders[i - 1].Number + 1).WithHash(blockHashes[i])
                        .TestObject;

                _testHeaderMapping[startHeader.Number + i] = blockHeaders[i].Hash;

                BlockHeader header = consistent
                    ? blockHeaders[i]
                    : blockHeaders[i - 1];

                BlockBuilder blockBuilder = Build.A.Block.WithHeader(header);

                if (withTransactions && header.ReceiptsRoot != Keccak.EmptyTreeHash)
                {
                    blockBuilder.WithTransactions(Build.A.Transaction.WithValue(i * 2).SignedAndResolved().TestObject,
                        Build.A.Transaction.WithValue(i * 2 + 1).SignedAndResolved().TestObject);
                }

                Block block = blockBuilder.TestObject;
                blockBodies[i] = new BlockBody(block.Transactions, block.Uncles);
                _bodies[blockHashes[i]] = blockBodies[i];

                if (allKnown)
                {
                    _blockTree.SuggestBlock(block);
                }
            }
        }

        BlockBodiesMessage message = new(blockBodies);
        byte[] messageSerialized = _bodiesSerializer.Serialize(message);
        return await Task.FromResult(_bodiesSerializer.Deserialize(messageSerialized).Bodies);
    }

    public async Task<TxReceipt[][]> BuildReceiptsResponse(IList<Keccak> blockHashes,
        SyncResponse flags = SyncResponse.AllCorrect)
    {
        TxReceipt[][] receipts = new TxReceipt[blockHashes.Count][];
        for (int i = 0; i < receipts.Length; i++)
        {
            BlockBody body = _bodies[blockHashes[i]];
            receipts[i] = body.Transactions
                .Select(t => Build.A.Receipt
                    .WithStatusCode(StatusCode.Success)
                    .WithGasUsed(10)
                    .WithBloom(Bloom.Empty)
                    .WithLogs(Build.A.LogEntry.WithAddress(t.SenderAddress).WithTopics(TestItem.KeccakA).TestObject)
                    .TestObject)
                .ToArray();

            _headers[blockHashes[i]].ReceiptsRoot = flags.HasFlag(SyncResponse.IncorrectReceiptRoot)
                ? Keccak.EmptyTreeHash
                : new ReceiptTrie(MainnetSpecProvider.Instance.GetSpec(_headers[blockHashes[i]].Number), receipts[i])
                    .RootHash;
        }

        ReceiptsMessage message = new(receipts);
        byte[] messageSerialized = _receiptsSerializer.Serialize(message);
        return await Task.FromResult(_receiptsSerializer.Deserialize(messageSerialized).TxReceipts);
    }
}
