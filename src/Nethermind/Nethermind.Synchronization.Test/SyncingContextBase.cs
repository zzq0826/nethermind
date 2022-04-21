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
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Synchronization.Peers;
using Nethermind.Synchronization.Test.Mocks;
using NUnit.Framework;

namespace Nethermind.Synchronization.Test;

public abstract class SyncingContextBase
{
    public static readonly ConcurrentQueue<SyncingContextBase> _allInstances = new();

    private readonly Dictionary<string, ISyncPeer> _peers = new();
    protected abstract BlockTree BlockTree { get; }

    protected abstract ISyncServer SyncServer { get; }

    protected abstract ISynchronizer Synchronizer { get; }

    protected abstract ISyncPeerPool SyncPeerPool { get; }

    protected readonly ILogManager _logManager = new OneLoggerLogManager(new ConsoleAsyncLogger(LogLevel.Debug));

    protected ILogger _logger;

    private static readonly Block _genesisBlock = Build.A.Block.Genesis.WithDifficulty(100000)
        .WithTotalDifficulty((UInt256)100000).TestObject;

    private const int Moment = 50;
    private const int WaitTime = 500;
    private const int DynamicTimeout = 5000;

    protected void SynchronizerOnSyncEvent(object sender, SyncEventArgs e)
    {
        TestContext.WriteLine(e.SyncEvent);
    }

    public SyncingContextBase BestKnownNumberIs(long number)
    {
        Assert.AreEqual(number, BlockTree.BestKnownNumber, "best known number");
        return this;
    }

    public SyncingContextBase BlockIsKnown()
    {
        Assert.True(BlockTree.IsKnownBlock(_blockHeader.Number, _blockHeader.Hash ?? _blockHeader.CalculateHash()),
            "block is known");
        return this;
    }

    public SyncingContextBase BestSuggestedHeaderIs(BlockHeader header)
    {
        int waitTimeSoFar = 0;
        _blockHeader = BlockTree.BestSuggestedHeader;
        while (header != _blockHeader && waitTimeSoFar <= DynamicTimeout)
        {
            _logger.Info($"ASSERTING THAT HEADER IS {header.Number} (WHEN ACTUALLY IS {_blockHeader?.Number})");
            Thread.Sleep(100);
            waitTimeSoFar += 100;
            _blockHeader = BlockTree.BestSuggestedHeader;
        }

        Assert.AreSame(header, _blockHeader, "header");
        return this;
    }

    public SyncingContextBase BestSuggestedBlockHasNumber(long number)
    {
        _logger.Info($"ASSERTING THAT NUMBER IS {number}");

        int waitTimeSoFar = 0;
        _blockHeader = BlockTree.BestSuggestedHeader;
        while (number != _blockHeader?.Number && waitTimeSoFar <= DynamicTimeout)
        {
            Thread.Sleep(10);
            waitTimeSoFar += 10;
            _blockHeader = BlockTree.BestSuggestedHeader;
        }

        Assert.AreEqual(number, _blockHeader?.Number, "block number");
        return this;
    }

    public SyncingContextBase BlockIsSameAsGenesis()
    {
        Assert.AreSame(BlockTree.Genesis, _blockHeader, "genesis");
        return this;
    }

    private BlockHeader _blockHeader;

    public SyncingContextBase Genesis
    {
        get
        {
            _blockHeader = BlockTree.Genesis;
            return this;
        }
    }

    private SyncingContextBase Wait(int milliseconds)
    {
        if (_logger.IsInfo) _logger.Info($"WAIT {milliseconds}");
        Thread.Sleep(milliseconds);
        return this;
    }

    public SyncingContextBase Wait()
    {
        return Wait(WaitTime);
    }

    public SyncingContextBase WaitUntilInitialized()
    {
        SpinWait.SpinUntil(() => SyncPeerPool.AllPeers.All(p => p.IsInitialized), DynamicTimeout);
        return this;
    }

    public SyncingContextBase After(Action action)
    {
        action();
        return this;
    }

    public SyncingContextBase BestSuggested
    {
        get
        {
            _blockHeader = BlockTree.BestSuggestedHeader;
            return this;
        }
    }

    public SyncingContextBase AfterProcessingGenesis()
    {
        Block genesis = _genesisBlock;
        BlockTree.SuggestBlock(genesis);
        BlockTree.UpdateMainChain(genesis);
        return this;
    }

    public SyncingContextBase AfterPeerIsAdded(ISyncPeer syncPeer)
    {
        ((SyncPeerMock)syncPeer).Disconnected += (_, _) => SyncPeerPool.RemovePeer(syncPeer);

        _logger.Info($"PEER ADDED {syncPeer.ClientId}");
        _peers.TryAdd(syncPeer.ClientId, syncPeer);
        SyncPeerPool.AddPeer(syncPeer);
        return this;
    }

    public SyncingContextBase AfterPeerIsRemoved(ISyncPeer syncPeer)
    {
        _peers.Remove(syncPeer.ClientId);
        SyncPeerPool.RemovePeer(syncPeer);
        return this;
    }

    public SyncingContextBase AfterNewBlockMessage(Block block, ISyncPeer peer)
    {
        _logger.Info($"NEW BLOCK MESSAGE {block.Number}");
        block.Header.TotalDifficulty = block.Difficulty * (ulong)(block.Number + 1);
        SyncServer.AddNewBlock(block, peer);
        return this;
    }

    public SyncingContextBase AfterHintBlockMessage(Block block, ISyncPeer peer)
    {
        _logger.Info($"HINT BLOCK MESSAGE {block.Number}");
        SyncServer.HintBlock(block.Hash ?? block.CalculateHash(), block.Number, peer);
        return this;
    }

    public SyncingContextBase PeerCountIs(long i)
    {
        Assert.AreEqual(i, SyncPeerPool.AllPeers.Count(), "peer count");
        return this;
    }

    public SyncingContextBase WaitAMoment()
    {
        return Wait(Moment);
    }

    public void Stop()
    {
        Synchronizer.SyncEvent -= SynchronizerOnSyncEvent;
        Task task = new(() =>
        {
            Task.WaitAll(new[] {Synchronizer.StopAsync(), SyncPeerPool.StopAsync()});
        });

        task.RunSynchronously();
    }
}
