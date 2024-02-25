// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Nethermind.Consensus;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Logging;
using Nethermind.Network.Contract.P2P;
using Nethermind.Network.P2P.Subprotocols.Eth.V62.Messages;
using Nethermind.Network.P2P.Subprotocols.Eth.V66.Messages;
using Nethermind.Network.P2P.Subprotocols.Eth.V67;
using Nethermind.Network.P2P.Subprotocols.Eth.V68.Messages;
using Nethermind.Network.Rlpx;
using Nethermind.Stats;
using Nethermind.Synchronization;
using Nethermind.TxPool;

namespace Nethermind.Network.P2P.Subprotocols.Eth.V68;

public class Eth68ProtocolHandler : Eth67ProtocolHandler
{
    private readonly ISession session;
    private readonly IPooledTxsRequestor _pooledTxsRequestor;

    private readonly Action<V66.Messages.GetPooledTransactionsMessage> _sendAction;
    private readonly LruCache<ValueHash256, (int, TxType)> _announceData = new (64 * 1024, 1042, "tx announce data");
    public override string Name => "eth68";

    public override byte ProtocolVersion => EthVersions.Eth68;

    public Eth68ProtocolHandler(ISession session,
        IMessageSerializationService serializer,
        INodeStatsManager nodeStatsManager,
        ISyncServer syncServer,
        ITxPool txPool,
        IPooledTxsRequestor pooledTxsRequestor,
        IGossipPolicy gossipPolicy,
        ForkInfo forkInfo,
        ILogManager logManager,
        ITxGossipPolicy? transactionsGossipPolicy = null)
        : base(session, serializer, nodeStatsManager, syncServer, txPool, pooledTxsRequestor, gossipPolicy, forkInfo, logManager, transactionsGossipPolicy)
    {
        this.session = session;
        _pooledTxsRequestor = pooledTxsRequestor;

        // Capture Action once rather than per call
        _sendAction = Send<V66.Messages.GetPooledTransactionsMessage>;
    }

    public override void HandleMessage(ZeroPacket message)
    {
        int size = message.Content.ReadableBytes;
        switch (message.PacketType)
        {
            case Eth68MessageCode.NewPooledTransactionHashes:
                if (CanReceiveTransactions)
                {
                    NewPooledTransactionHashesMessage68 newPooledTxHashesMsg =
                        Deserialize<NewPooledTransactionHashesMessage68>(message.Content);
                    ReportIn(newPooledTxHashesMsg, size);
                    Handle(newPooledTxHashesMsg);
                }
                else
                {
                    const string ignored = $"{nameof(NewPooledTransactionHashesMessage68)} ignored, syncing";
                    ReportIn(ignored, size);
                }

                break;
            default:
                base.HandleMessage(message);
                break;
        }
    }

    protected override void Handle(TransactionsMessage msg)
    {
        //We can disconnect before or after accepting tx?
        base.Handle(msg);
        ValidateAnnouncedValues(msg);
    }

    private void ValidateAnnouncedValues(TransactionsMessage msg)
    {
        foreach (var tx in msg.Transactions)
        {
            Debug.Assert(tx.Hash != null);
            if (_announceData.Contains(tx.Hash))
            {
                (int size, TxType type) = _announceData.Get(tx.Hash);
                Logger.Info($"validating announce data {tx.Hash} {size} {type}");
                Logger.Info($"transaction size diff: {Math.Abs(size - tx.GetLength())} Type: {type} {tx.Type}");

                if (new Random((int)DateTime.UtcNow.Ticks).NextSingle() < 0.1)
                {
                    throw new Exception($"throw exeption for: {session.Node.Address}.");
                }
                if (type != tx.Type)
                {
                    throw new SubprotocolException($"Announced tx type mismatch.");
                }

                //Geth gives some leeway in size difference
                //https://github.com/ethereum/go-ethereum/blob/master/eth/fetcher/tx_fetcher.go#L596
                if (Math.Abs(size - tx.GetLength()) > 8)
                {
                    throw new SubprotocolException($"Announced tx size mismatch.");
                }
                _announceData.Delete(tx.Hash);
            }
        }
    }

    private void Handle(NewPooledTransactionHashesMessage68 message)
    {
        bool isTrace = Logger.IsTrace;
        if (message.Hashes.Count != message.Types.Count || message.Hashes.Count != message.Sizes.Count)
        {
            string errorMessage = $"Wrong format of {nameof(NewPooledTransactionHashesMessage68)} message. " +
                                  $"Hashes count: {message.Hashes.Count} " +
                                  $"Types count: {message.Types.Count} " +
                                  $"Sizes count: {message.Sizes.Count}";
            if (isTrace) Logger.Trace(errorMessage);

            throw new SubprotocolException(errorMessage);
        }

        Metrics.Eth68NewPooledTransactionHashesReceived++;
        TxPool.Metrics.PendingTransactionsHashesReceived += message.Hashes.Count;

        AddNotifiedTransactions(message.Hashes);

        Stopwatch? stopwatch = isTrace ? Stopwatch.StartNew() : null;

        IReadOnlyList<TxAnnounceData> requested = _pooledTxsRequestor.RequestTransactionsEth68(_sendAction, message.Hashes, message.Sizes, message.Types);
        
        foreach (TxAnnounceData announceData in requested)
        {
            //We do not want to overwrite previous announcement values to prevent manipulation from the peer
            if (!_announceData.Contains(announceData.Hash))
            {
                Logger.Info($"insert announce data {announceData.Hash} {announceData.Size} {announceData.Type}" );
                _announceData.Set(announceData.Hash, (announceData.Size, announceData.Type));
            }
        }

        stopwatch?.Stop();

        if (isTrace) Logger.Trace($"OUT {Counter:D5} {nameof(NewPooledTransactionHashesMessage68)} to {Node:c} in {stopwatch.Elapsed.TotalMilliseconds}ms");
    }

    protected override void SendNewTransactionCore(Transaction tx)
    {
        if (tx.CanBeBroadcast())
        {
            base.SendNewTransactionCore(tx);
        }
        else
        {
            SendMessage(new[] { (byte)tx.Type }, new int[] { tx.GetLength() }, new Hash256[] { tx.Hash });
        }
    }

    protected override void SendNewTransactionsCore(IEnumerable<Transaction> txs, bool sendFullTx)
    {
        if (sendFullTx)
        {
            base.SendNewTransactionsCore(txs, sendFullTx);
            return;
        }

        using ArrayPoolList<byte> types = new(NewPooledTransactionHashesMessage68.MaxCount);
        using ArrayPoolList<int> sizes = new(NewPooledTransactionHashesMessage68.MaxCount);
        using ArrayPoolList<Hash256> hashes = new(NewPooledTransactionHashesMessage68.MaxCount);

        foreach (Transaction tx in txs)
        {
            if (hashes.Count == NewPooledTransactionHashesMessage68.MaxCount)
            {
                SendMessage(types, sizes, hashes);
                types.Clear();
                sizes.Clear();
                hashes.Clear();
            }

            if (tx.Hash is not null)
            {
                types.Add((byte)tx.Type);
                sizes.Add(tx.GetLength());
                hashes.Add(tx.Hash);
                TxPool.Metrics.PendingTransactionsHashesSent++;
            }
        }

        if (hashes.Count != 0)
        {
            SendMessage(types, sizes, hashes);
        }
    }

    public void SendMessage(IReadOnlyList<byte> types, IReadOnlyList<int> sizes, IReadOnlyList<Hash256> hashes)
    {
        NewPooledTransactionHashesMessage68 message = new(types, sizes, hashes);
        Metrics.Eth68NewPooledTransactionHashesSent++;
        Send(message);
    }
}
