// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Consensus.Tracing
{
    public interface IDebugStyleTracer
    {
        GethLikeTxTrace Trace(Keccak txHash, CancellationToken cancellationToken);
        GethLikeTxTrace Trace(long blockNumber, Transaction transaction, CancellationToken cancellationToken);
        GethLikeTxTrace Trace(long blockNumber, int txIndex, CancellationToken cancellationToken);
        GethLikeTxTrace Trace(Keccak blockHash, int txIndex, CancellationToken cancellationToken);
        GethLikeTxTrace Trace(Rlp blockRlp, Keccak txHash, CancellationToken cancellationToken);
        GethLikeTxTrace? Trace(BlockParameter blockParameter, Transaction tx, CancellationToken cancellationToken);
        GethLikeTxTrace[] TraceBlock(BlockParameter blockParameter, CancellationToken cancellationToken);
        GethLikeTxTrace[] TraceBlock(Rlp blockRlp, CancellationToken cancellationToken);
    }
}
