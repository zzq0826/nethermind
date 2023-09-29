// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Buffers;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Core;

/// <summary>
/// A holder for Block that must be explicitly disposed or there will be memory leak. May uses netty's buffer or
/// rocksdb's buffer directly. Block may contain `Memory<byte>` that is explicitly managed. Reusing `Block` or tx from
/// this object after Dispose is likely to cause corrupted `Block`. Disposing multiple time does nothing.
/// </summary>
public class OwnedBlock : IDisposable
{
    private Block _block = null;
    private IMemoryOwner<byte>? _memoryOwner = null;

    public OwnedBlock(Block bodies, IMemoryOwner<byte>? memoryOwner = null)
    {
        _block = bodies;
        _memoryOwner = memoryOwner;
    }

    public Block Block => _block;

    /// <summary>
    /// Disown the `Block`, copying any `Memory<byte>` so that it does not depend on the `_memoryOwner.`
    /// </summary>
    public void Disown()
    {
        if (_memoryOwner == null) return;

        foreach (Transaction tx in _block.Transactions)
        {
            Keccak? _ = tx.Hash; // Just need to trigger hash calculation
            if (tx.Data != null)
            {
                tx.Data = tx.Data.Value.ToArray();
            }
        }

        _memoryOwner?.Dispose();
        _memoryOwner = null;
    }

    public void Dispose()
    {
        if (_memoryOwner == null) return;

        foreach (Transaction tx in _block.Transactions)
        {
            TxDecoder.TxObjectPool.Return(tx);
        }

        _memoryOwner?.Dispose();
        _memoryOwner = null;
    }
}
