// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only
using Nethermind.Core.Crypto;
using Nethermind.Trie;

namespace Nethermind.Verkle;

public partial class VerkleTree
{
    public void Accept(ITreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null)
    {
        if (visitor is null) throw new ArgumentNullException(nameof(visitor));
        if (rootHash is null) throw new ArgumentNullException(nameof(rootHash));
        visitingOptions ??= VisitingOptions.Default;

        using TrieVisitContext trieVisitContext = new TrieVisitContext
        {
            // hacky but other solutions are not much better, something nicer would require a bit of thinking
            // we introduced a notion of an account on the visit context level which should have no knowledge of account really
            // but we know that we have multiple optimizations and assumptions on trees
            ExpectAccounts = visitingOptions.ExpectAccounts,
            MaxDegreeOfParallelism = visitingOptions.MaxDegreeOfParallelism
        };

        if (!rootHash.Equals(Keccak.EmptyTreeHash))
        {
            _stateDb.MoveToStateRoot(rootHash.Bytes);
        }
        else
        {
            return;
        }

        if (visitor is RootCheckVisitor)
        {
            if (!rootHash.Bytes.SequenceEqual(_stateDb.GetStateRoot())) visitor.VisitMissingNode(Keccak.Zero, trieVisitContext);
        }
        else
        {
            throw new Exception();
        }

    }
}
