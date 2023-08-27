// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Buffers;
using Nethermind.State.Snap;

namespace Nethermind.Network.P2P.Subprotocols.Snap.Messages
{
    public class StorageRangeMessage : SnapMessageBase, IDisposable
    {
        public override int PacketType => SnapMessageCode.StorageRanges;

        public IMemoryOwner<byte>? MemoryOwner { get; set; }

        /// <summary>
        /// List of list of consecutive slots from the trie (one list per account)
        /// </summary>
        public PathWithStorageSlot[][] Slots { get; set; }

        /// <summary>
        /// List of trie nodes proving the slot range
        /// </summary>
        public byte[][] Proofs { get; set; }

        public void Dispose()
        {
            MemoryOwner?.Dispose();
        }
    }
}
