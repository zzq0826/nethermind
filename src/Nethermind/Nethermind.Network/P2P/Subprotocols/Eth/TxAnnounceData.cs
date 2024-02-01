// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Stats.Model;

namespace Nethermind.Network.P2P.Subprotocols.Eth;

public struct TxAnnounceData
{
    public TxAnnounceData(Hash256 hash, int size, TxType type)
    {
        Hash = hash;
        Size = size;
        Type = type;
    }

    public Hash256 Hash { get; }
    public int Size { get; }
    public TxType Type { get; }
}
