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

using System.Threading.Tasks;
using FluentAssertions;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Db;
using Nethermind.JsonRpc;
using Nethermind.Merge.Plugin.Data;
using Nethermind.Merge.Plugin.Data.V1;
using Nethermind.Merge.Plugin.Synchronization;
using Nethermind.Synchronization;
using NUnit.Framework;

namespace Nethermind.Merge.Plugin.Test;

public partial class EngineModuleTests
{
    [Test]
    public async Task newPayloadV1_can_insert_blocks_from_cache_when_syncing()
    {
        EngineSynchronizationScenario.ScenarioBuilder scenario = EngineSynchronizationScenario.GoesLikeThis()
            .CreateRpcModule(10, 20)
            .SendPayloadRequests(13, 18, PayloadStatus.Accepted)
            .AssertInBeaconHeaders(false)
            .AssertBestPointers(9, 9, 9, 0)
            .AssertLowestHeaders(null, null)
            .SetBeaconPivot(17)
            .AssertInBeaconHeaders(true)
            .AssertBestPointers(9, 9, 9, 0)
            .SendPayloadRequest(19, PayloadStatus.Syncing)
            .AssertBeaconBlocksInserted(17, 19)
            .AssertInBeaconHeaders(true)
            .AssertBestPointers(9, 9, 9, 19)
            .AssertLowestHeaders(17, 17);
        await scenario.Finish();
    }

    [Test]
    public async Task Blocks_from_cache_inserted_when_fast_headers_sync_finish_before_newPayloadV1_request()
    {
        ISyncConfig syncConfig = new SyncConfig
        {
            FastSync = true,
            FastBlocks = true,
            PivotNumber = "10",
        };
        EngineSynchronizationScenario.ScenarioBuilder scenario = EngineSynchronizationScenario.GoesLikeThis()
            .CreateRpcModule(1, 20, syncConfig)
            .SendPayloadRequests(13, 18, PayloadStatus.Accepted)
            .AssertInBeaconHeaders(false)
            .AssertBestPointers(0, 0, 0, 0)
            .AssertLowestHeaders(null, null)
            .SetBeaconPivot(17)
            .FillInHeaders(15, 17)
            .AssertInBeaconHeaders(true)
            .FillInHeaders(11, 14)
            .AssertInBeaconHeaders(false)
            .AssertBestPointers(0, 0, 0, 17)
            .AssertLowestHeaders(11, 11)
            .SendPayloadRequest(19, PayloadStatus.Syncing)
            .AssertBeaconBlocksInserted(17, 19)
            .AssertBestPointers(0, 0, 0, 19);
        await scenario.Finish();
    }
    
    [Test]
    public async Task Maintain_correct_pointers_for_beacon_sync_in_archive_sync()
    {
        EngineSynchronizationScenario.ScenarioBuilder scenario = EngineSynchronizationScenario.GoesLikeThis()
            .CreateRpcModule(1, 20)
            .SendPayloadRequests(13, 17, PayloadStatus.Accepted)
            .SetBeaconPivot(15)
            .SetForkchoice(18, PayloadStatus.Syncing)
            .SendPayloadRequest(18, PayloadStatus.Syncing)
            .FillInHeaders(1, 15)
            .AssertInBeaconHeaders(false)
            .AssertBestPointers(0, 0, 0, 18)
            .AssertLowestHeaders(1, 1)
            .ProcessBlocks(1, 18)
            .AssertBestPointers(18, 18, 18, 18) // TODO: investigate why failing
            .SendPayloadRequest(19, PayloadStatus.Valid)
            .AssertBestPointers(19, 19, 19, 19);
        await scenario.Finish();
    }

    private static class EngineSynchronizationScenario
    {
        public class ScenarioBuilder
        {
            private IBlockTree? _syncedTree;
            private IBlockTree? _notSyncedTree;
            private IBeaconPivot? _beaconPivot;
            private IBeaconSyncStrategy? _beaconSync;
            private IEngineRpcModule? _rpc;
            private Task<ScenarioBuilder>? _antecedent;

            private BlockTreeInsertOptions _insertOptions = BlockTreeInsertOptions.All;

            public ScenarioBuilder CreateRpcModule(int treeSize, int syncedTreeSize, ISyncConfig? syncConfig = null)
            {
                _antecedent = CreateRpcModuleAsync(treeSize, syncedTreeSize, syncConfig);
                return this;
            }
            
            public ScenarioBuilder SetBeaconPivot(int pivotNum)
            {
                _antecedent = SetBeaconPivotAsync(pivotNum);
                return this;
            }
            
            public ScenarioBuilder SendPayloadRequests(int lower, int higher, string expectedStatus)
            {
                _antecedent = SendPayloadRequestsAsync(lower, higher, expectedStatus);
                return this;
            }
            
            public ScenarioBuilder SendPayloadRequest(int blockNum, string expectedStatus)
            {
                _antecedent = SendPayloadRequestsAsync(blockNum, blockNum, expectedStatus);
                return this;
            }

            public ScenarioBuilder SetForkchoice(int blockNum, string expectedStatus)
            {
                _antecedent = SetForkchoicesAsync(blockNum, blockNum, expectedStatus);
                
                return this;
            }

            public ScenarioBuilder FillInHeaders(int lower, int higher)
            {
                _antecedent = FillInHeadersAsync(lower, higher);
                
                return this;
            }
            
            public ScenarioBuilder ProcessBlocks(int lower, int higher)
            {
                _antecedent = ProcessBlocksAsync(lower, higher);
                
                return this;
            }

            public ScenarioBuilder AssertInBeaconHeaders(bool syncing)
            {
                _antecedent = AssertInBeaconHeadersAsync(syncing);

                return this;
            }
            
            public ScenarioBuilder AssertBeaconBlocksInserted(int lower, int higher)
            {
                _antecedent = AssertBeaconBlocksInsertedAsync(lower, higher);
                
                return this;
            }

            public ScenarioBuilder AssertBestPointers(long bestKnown, long bestHeader, long bestBlock,
                long bestBeaconBlock)
            {
                _antecedent = AssertBestPointersAsync(bestKnown, bestHeader, bestBlock, bestBeaconBlock);

                return this;
            }

            public ScenarioBuilder AssertLowestHeaders(long? lowestInserted, long? lowestInsertedBeacon)
            {
                _antecedent = AssertLowestHeadersAsync(lowestInserted, lowestInsertedBeacon);

                return this;
            }

            private void BuildBlockTrees(MergeTestBlockchain chain, int notSyncedTreeSize = 0, int syncedTreeSize = 0)
            {
                Block current = chain.BlockTree.FindGenesisBlock()!;
                Block[] blocks = new Block[notSyncedTreeSize];
                _notSyncedTree = chain.BlockTree;
                if (notSyncedTreeSize > 0)
                {
                    for (int i = 0; i < notSyncedTreeSize; i++)
                    {
                        blocks[i] = current; 
                        if (!(current.IsGenesis))
                        {
                            AddBlockResult result = chain.BlockTree.SuggestBlock(current);
                            Assert.AreEqual(AddBlockResult.Added, result);
                            
                            chain.BlockTree.UpdateMainChain(new[] {current}, true);
                        }
                        
                        chain.State.Commit(chain.SpecProvider.GetSpec(i + 1));
                        chain.State.RecalculateStateRoot();
                        Block parent = current;
                        current = Build.A.Block
                            .WithNumber(i + 1)
                            .WithParent(parent)
                            .WithStateRoot(chain.State.StateRoot)
                            .TestObject;
                    }
                }

                if (syncedTreeSize > 0)
                {
                    _syncedTree = Build.A.BlockTree().WithBlocks(blocks).TestObject;
                    for (int i = notSyncedTreeSize; i < syncedTreeSize; i++)
                    {
                        if (!(current.IsGenesis))
                        {
                            AddBlockResult result = _syncedTree.SuggestBlock(current);
                            Assert.AreEqual(AddBlockResult.Added, result);
                    
                            _syncedTree.UpdateMainChain(new[] { current }, true);
                        }
                        
                        chain.State.Commit(chain.SpecProvider.GetSpec(i + 1));
                        chain.State.RecalculateStateRoot();
                        Block parent = current;
                        current = Build.A.Block
                            .WithNumber(i + 1)
                            .WithParent(parent)
                            .WithDifficulty(0)
                            .WithNonce(0)
                            .WithPostMergeFlag(true)
                            .WithStateRoot(chain.State.StateRoot)
                            .TestObject;
                    }
                }
            }

            private async Task<ScenarioBuilder> CreateRpcModuleAsync(int treeSize, int syncedTreeSize, ISyncConfig? syncConfig = null)
            {
                await ExecuteAntecedentIfNeeded();
                
                using MergeTestBlockchain chain = await CreateBlockChain();
                BuildBlockTrees(chain, treeSize,  syncedTreeSize);
                _rpc = CreateEngineModule(chain, syncConfig);
                _beaconPivot = chain.BeaconPivot;
                _beaconSync = chain.BeaconSync;

                return this;
            }

            private async Task<ScenarioBuilder> SetBeaconPivotAsync(int pivotNum)
            {
                await ExecuteAntecedentIfNeeded(); 
                await SendPayloadRequestAsync(pivotNum, PayloadStatus.Accepted);
                await SetForkchoiceAsync(pivotNum, PayloadStatus.Syncing);
                AssertBeaconPivotValues(pivotNum);
                return this;
            }

            private async Task<ScenarioBuilder> SendPayloadRequestsAsync(int lower, int higher, string expectedStatus)
            {
                await ExecuteAntecedentIfNeeded();
                for (int i = lower; i <= higher; i++)
                {
                    await SendPayloadRequestAsync(i, expectedStatus);
                }

                return this;
            }
            
            private async Task<ScenarioBuilder> SetForkchoicesAsync(int lower, int higher, string expectedStatus)
            {
                await ExecuteAntecedentIfNeeded();
                for (int i = lower; i <= higher; i++)
                {
                    await SetForkchoiceAsync(i, expectedStatus);
                }

                return this;
            }

            private async Task SendPayloadRequestAsync(int blockNum, string expectedStatus)
            {
                Block? beaconBlock = _syncedTree!.FindBlock(blockNum, BlockTreeLookupOptions.None);
                BlockRequestResult request = new (beaconBlock!);
                ResultWrapper<PayloadStatusV1> payloadStatus = await _rpc!.engine_newPayloadV1(request);
                payloadStatus.Data.Status.Should().Be(expectedStatus);
                AssertInBeaconSync(expectedStatus == PayloadStatus.Syncing, beaconBlock!.Header);
            }

            private async Task SetForkchoiceAsync(int blockNum, string expectedStatus)
            {
                Block beaconBlock = _syncedTree!.FindBlock(blockNum, BlockTreeLookupOptions.None)!;
                Keccak headHash = _notSyncedTree!.HeadHash;
                ForkchoiceStateV1 forkchoiceStateV1 = new(beaconBlock.Hash!, headHash, headHash);
                ResultWrapper<ForkchoiceUpdatedV1Result> forkchoiceUpdatedResult =
                    await _rpc!.engine_forkchoiceUpdatedV1(forkchoiceStateV1);
                forkchoiceUpdatedResult.Data.PayloadStatus.Status.Should()
                    .Be(expectedStatus);

                BlockHeader? requestHeader = _notSyncedTree.FindHeader(blockNum, BlockTreeLookupOptions.None);
                AssertInBeaconSync(expectedStatus == PayloadStatus.Syncing, requestHeader);
                AssertExecutionStatusChanged(expectedStatus == PayloadStatus.Valid, beaconBlock.Hash!, headHash, headHash);
            }

            private async Task<ScenarioBuilder> FillInHeadersAsync(int lower, int higher)
            {
                await ExecuteAntecedentIfNeeded();
                
                for (long i = higher; i >= lower; --i)
                {
                    BlockHeader? beaconHeader = _syncedTree!.FindHeader(i, BlockTreeLookupOptions.TotalDifficultyNotNeeded);
                    AddBlockResult insertResult = _notSyncedTree!.Insert(beaconHeader!, _insertOptions);
                    Assert.AreEqual(AddBlockResult.Added, insertResult);
                }

                return this;
            }

            private async Task<ScenarioBuilder> ProcessBlocksAsync(int lower, int higher)
            {
                await ExecuteAntecedentIfNeeded();
                
                for (long i = lower; i <= higher; i++)
                {
                    Block? beaconBlock = _syncedTree!.FindBlock(i, BlockTreeLookupOptions.None);
                    AddBlockResult insertResult = await _notSyncedTree!.SuggestBlockAsync(beaconBlock!);
                    Assert.AreEqual(AddBlockResult.Added, insertResult);
                    
                    _notSyncedTree.UpdateMainChain(new[] {beaconBlock!}, true);
                }
                
                return this;
            }

            private async Task<ScenarioBuilder> AssertBeaconBlocksInsertedAsync(int lower, int higher)
            {
                await ExecuteAntecedentIfNeeded();
                
                for (long i = lower; i <= higher; i++)
                {
                    Keccak hash = _syncedTree!.FindHash(i);
                    _notSyncedTree!.FindBlock(i, BlockTreeLookupOptions.None)?.Hash.Should().BeEquivalentTo(hash);
                    _notSyncedTree.FindHeader(i, BlockTreeLookupOptions.None)?.Hash.Should().BeEquivalentTo(hash);
                }
                
                return this;
            }
            
            private void AssertBeaconPivotValues(int pivotNum)
            {
                BlockHeader blockHeader = _syncedTree!.FindHeader(pivotNum, BlockTreeLookupOptions.None)!;
                _beaconPivot!.BeaconPivotExists().Should().BeTrue();
                _beaconPivot.PivotNumber.Should().Be(blockHeader!.Number);
                _beaconPivot.PivotHash.Should().Be(blockHeader.Hash ?? blockHeader.CalculateHash());
                _beaconPivot.PivotTotalDifficulty.Should().Be(null);
            }

            private async Task<ScenarioBuilder> AssertInBeaconHeadersAsync(bool syncing)
            {
                await ExecuteAntecedentIfNeeded();
                
                _beaconSync!.ShouldBeInBeaconHeaders().Should().Be(syncing);
                _beaconSync.IsBeaconSyncHeadersFinished().Should().Be(!syncing);

                return this;
            }

            private void AssertInBeaconSync(bool syncing, BlockHeader? header)
            {
                _beaconSync!.IsBeaconSyncFinished(header).Should().Be(!syncing);
            }

            private async Task<ScenarioBuilder> AssertBestPointersAsync(long bestKnown, long bestHeader, long bestBlock,
                long bestBeaconBlock)
            {
                await ExecuteAntecedentIfNeeded();
                
                AssertBestKnownNumber(bestKnown);
                AssertBestSuggestedHeader(bestHeader);
                AssertBestSuggestedBody(bestBlock);
                AssertBestBeaconBlock(bestBeaconBlock);

                return this;
            }

            private async Task<ScenarioBuilder> AssertLowestHeadersAsync(long? lowestInserted, long? lowestInsertedBeacon)
            {
                await ExecuteAntecedentIfNeeded();
                
                AssertLowestInsertedHeader(lowestInserted);
                AssertLowestInsertedBeaconHeader(lowestInsertedBeacon);

                return this;
            }
            
            private void AssertBestKnownNumber(long expected)
            {
                Assert.AreEqual(expected,_notSyncedTree!.BestKnownNumber);
            }
            
            private void AssertBestSuggestedHeader(long expected)
            {
                Assert.AreEqual(_syncedTree!.FindHeader(expected, BlockTreeLookupOptions.None)?.Hash,_notSyncedTree!.BestSuggestedHeader?.Hash);
            }
            
            private void AssertBestSuggestedBody(long expected)
            {
                Assert.AreEqual(_syncedTree!.FindBlock(expected, BlockTreeLookupOptions.None)?.Hash,_notSyncedTree!.BestSuggestedBody.Hash);
            }
            
            private void AssertBestBeaconBlock(long expected)
            {
                Assert.AreEqual(expected,_notSyncedTree!.BestSuggestedBeaconHeader?.Number ?? 0);
            }
            
            public void AssertLowestInsertedBeaconHeader(long? expected)
            {
                Assert.IsNotNull(_notSyncedTree);

                if (expected.HasValue)
                {
                    BlockHeader? lowestHeader = _syncedTree!.FindHeader(expected.Value, BlockTreeLookupOptions.None);
                    Assert.AreEqual(lowestHeader?.Hash, _notSyncedTree!.LowestInsertedBeaconHeader?.Hash);
                }
                else
                {
                    Assert.IsNull(_notSyncedTree!.LowestInsertedBeaconHeader);
                }
            }
            
            private void AssertLowestInsertedHeader(long? expected)
            {
                Assert.IsNotNull(_notSyncedTree);
                if (expected.HasValue)
                {
                    BlockHeader? lowestHeader = _syncedTree!.FindHeader(expected.Value, BlockTreeLookupOptions.None);
                    Assert.AreEqual(lowestHeader?.Hash, _notSyncedTree!.LowestInsertedHeader?.Hash);
                }
                else
                {
                    Assert.IsNull(_notSyncedTree!.LowestInsertedHeader);
                }
            }
            
            private void AssertExecutionStatusChanged(bool changed, Keccak headBlockHash, Keccak finalizedBlockHash, Keccak safeBlockHash)
            {
                ExecutionStatusResult result = _rpc!.engine_executionStatus().Data;
                if (changed)
                {
                    Assert.AreEqual(headBlockHash, result.HeadBlockHash);
                    Assert.AreEqual(finalizedBlockHash, result.FinalizedBlockHash);
                    Assert.AreEqual(safeBlockHash, result.SafeBlockHash);    
                }
                else
                {
                    Assert.AreNotEqual(headBlockHash, result.HeadBlockHash);
                    Assert.AreNotEqual(finalizedBlockHash, result.FinalizedBlockHash);
                    Assert.AreNotEqual(safeBlockHash, result.SafeBlockHash);
                }
            }
            
            public async Task Finish()
            {
                await ExecuteAntecedentIfNeeded();
            }

            private async Task ExecuteAntecedentIfNeeded()
            {
                if (_antecedent != null)
                {
                    await _antecedent;
                }
            }
        }
        
        public static ScenarioBuilder GoesLikeThis()
        {
            return new();
        }
    }
}
