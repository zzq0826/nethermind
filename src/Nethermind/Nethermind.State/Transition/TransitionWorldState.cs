// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.State.Transition;

// MERKLE -> VERKLE transition
// Start: We just have a merkle tree, and we are working on the merkle tree
// 1. we prepare the db to have a direct read access to the database for leaves without traversing the tree
// 2. we have a hard fork, and we started using the verkle tree as an overlay tree to do all the operations
// ---- we have the finalization of the hard fork block and every node has a set of preimages with them
// 3. after finalization, we start moving batch of leaves from merkle tree to verkle tree (LEAVES_TO_CONVERT)
// ---- everything is moved to verkle tree
// 4. starting using only the verkle tree only
// ---- finalization of the last conversion block happens
// 5. remove all the residual merkle state
// >>>> TRANSITION IS FINISHED


// idea - hide everything behind a single interface that manages both the merkle and verkle tree
// we can pass this interface across the client and this interface can act as the plugin we want
// but this plugin also needs input for the block we are currently on [ Flush(long blockNumber) ]
// and the spec that is being used [Flush(long blockNumber, IReleaseSpec releaseSpec)


// also design the interface in a way that we can do a proper archive sync ever after the transition


public class TransitionWorldState(
    IStateReader merkleStateReader,
    Hash256 finalizedStateRoot,
    VerkleStateTree verkleTree,
    IKeyValueStore codeDb,
    ILogManager? logManager)
    : VerkleWorldState(new TransitionStorageProvider(merkleStateReader, finalizedStateRoot, verkleTree, logManager),
        verkleTree,
        codeDb, logManager)
{
    private Hash256 FinalizedMerkleStateRoot { get; } = finalizedStateRoot;

    protected override Account? GetAndAddToCache(Address address)
    {
        if (_nullAccountReads.Contains(address)) return null;
        Account? account = GetState(address)?? merkleStateReader.GetAccountDefault(FinalizedMerkleStateRoot, address);
        if (account is not null)
        {
            PushJustCache(address, account);
        }
        else
        {
            // just for tracing - potential perf hit, maybe a better solution?
            _nullAccountReads.Add(address);
        }

        return account;
    }

    /// <summary>
    /// Technically, there is no use for doing this because it will anyway call the base class.
    /// But this is just a reminder that we don't try to get anything from a merkle tree here because
    /// GetCodeChunk is only called when you are running the stateless client and that is not supported
    /// while the transition is ongoing.
    /// Stateless clients can only work after the transition in complete?
    /// </summary>
    /// <param name="codeOwner"></param>
    /// <param name="chunkId"></param>
    /// <returns></returns>
    public override byte[] GetCodeChunk(Address codeOwner, UInt256 chunkId)
    {
        return base.GetCodeChunk(codeOwner, chunkId);
    }
}

