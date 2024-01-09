// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text;
using Nethermind.Core.Extensions;
using Nethermind.Verkle.Tree.Sync;

namespace Nethermind.Network.P2P.Subprotocols.Verkle.Messages;

public class SubTreeRangeMessage: VerkleMessageBase
{
    public override int PacketType => VerkleMessageCode.SubTreeRange;

    /// <summary>
    /// List of consecutive accounts from the trie
    /// </summary>
    public PathWithSubTree[] PathsWithSubTrees { get; set; }

    /// <summary>
    /// Verkle Proof
    /// </summary>
    public byte[] Proofs { get; set; }

    public override string ToString()
    {
        StringBuilder build = new();
        build.AppendLine(Proofs.ToHexString());
        foreach (PathWithSubTree? pwst in PathsWithSubTrees)
        {
            build.AppendLine($"{pwst.Path.ToString()}");
            foreach (var lst in pwst.SubTree)
            {
                build.AppendLine(lst.ToString());
            }
        }

        return build.ToString();
    }
}
