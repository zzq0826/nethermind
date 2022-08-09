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
using Nethermind.Consensus;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Logging;
using Nethermind.Network.P2P.Subprotocols.Eth.V65;
using Nethermind.Stats;
using Nethermind.Synchronization;
using Nethermind.TxPool;
using GetNodeDataMessage = Nethermind.Network.P2P.Subprotocols.Eth.V63.Messages.GetNodeDataMessage;
using NodeDataMessage = Nethermind.Network.P2P.Subprotocols.Eth.V63.Messages.NodeDataMessage;

namespace Nethermind.Network.P2P.Subprotocols.Eth.V67
{
    /// <summary>
    /// https://eips.ethereum.org/EIPS/eip-4938
    /// </summary>
    public class Eth67ProtocolHandler : Eth65ProtocolHandler
    {
        public Eth67ProtocolHandler(ISession session,
            IMessageSerializationService serializer,
            INodeStatsManager nodeStatsManager,
            ISyncServer syncServer,
            ITxPool txPool,
            IPooledTxsRequestor pooledTxsRequestor,
            IGossipPolicy gossipPolicy,
            ISpecProvider specProvider,
            ILogManager logManager)
            : base(session, serializer, nodeStatsManager, syncServer, txPool, pooledTxsRequestor, gossipPolicy, specProvider, logManager)
        {
        }

        public override string Name => "eth67";

        public override byte ProtocolVersion => 67;

        public override Task<byte[][]> GetNodeData(IReadOnlyList<Keccak> keys, CancellationToken token)
        {
            if (Logger.IsWarn) Logger.Warn($"{nameof(GetNodeData)} called on {Name} protocol.");
            return Task.FromResult(Array.Empty<byte[]>());
        }

        protected override void Handle(NodeDataMessage msg, int size)
        {
            if (Logger.IsWarn) Logger.Warn($"{nameof(Handle)} of {nameof(NodeDataMessage)} called on {Name} protocol.");
        }

        protected override void Handle(GetNodeDataMessage msg)
        {
            if (Logger.IsWarn) Logger.Warn($"{nameof(Handle)} of {nameof(GetNodeDataMessage)} called on {Name} protocol.");
        }

        protected override Task<byte[][]> SendRequest(GetNodeDataMessage message, CancellationToken token)
        {
            if (Logger.IsWarn) Logger.Warn($"{nameof(SendRequest)} for {nameof(GetNodeDataMessage)} called on {Name} protocol.");
            return Task.FromResult(Array.Empty<byte[]>());
        }
    }
}
