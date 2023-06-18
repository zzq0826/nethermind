// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethermind.Core.Crypto;

namespace Nethermind.State.Snap
{
    public class StorageRange
    {
        public long? BlockNumber { get; set; }

        /// <summary>
        /// Root hash of the account trie to serve
        /// </summary>
        public ValueKeccak RootHash { get; set; }

        /// <summary>
        /// Accounts of the storage tries to serve
        /// </summary>
        public PathWithAccount[] Accounts { get; set; }

        /// <summary>
        /// Account hash of the first to retrieve
        /// </summary>
        public ValueKeccak? StartingHash { get; set; }

        /// <summary>
        /// Account hash after which to stop serving data
        /// </summary>
        public ValueKeccak? LimitHash { get; set; }

        /// <summary>
        /// Mark this request as large storage
        /// </summary>
        public bool LargeStorage { get; set; }

        public override string ToString()
        {
            return $"StorageRange: ({BlockNumber}, {RootHash}, {StartingHash}, {LimitHash}, {LargeStorage})";
        }
    }
}
