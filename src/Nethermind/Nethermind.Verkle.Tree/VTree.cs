// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.CompilerServices;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Trie;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;
using Nethermind.Verkle.Tree.Utils;
using LeafUpdateDelta = Nethermind.Verkle.Tree.Utils.LeafUpdateDelta;

namespace Nethermind.Verkle.Tree;

public class VTree: IVerkleTree
{
    private byte[] _stateRoot;

    public byte[] StateRoot
    {
        get
        {
            if (_isDirty) throw new InvalidOperationException("trying to get root hash of not committed tree");
            return _stateRoot;
        }
        set
        {
            _stateRoot = value;
        }
    }

    private bool _isDirty;

    private readonly SortedDictionary<byte[], LeafUpdateDelta> _leafUpdateCache;
    private readonly VerkleMemoryDb _treeStore = new VerkleMemoryDb();

    public VTree()
    {
        _leafUpdateCache = new SortedDictionary<byte[], LeafUpdateDelta>();
        _stateRoot = new byte[32];
    }

    public bool MoveToStateRoot(byte[] stateRoot)
    {
        throw new NotImplementedException();
    }

    public byte[]? Get(byte[] key)
    {
#if DEBUG
        if (key.Length != 32) throw new ArgumentException("key must be 32 bytes", nameof(key));
#endif
        _treeStore.LeafTable.TryGetValue(key, out byte[]? leaf);
        return leaf;
    }

    public void Insert(byte[]key, byte[] value)
    {
        _isDirty = true;
#if DEBUG
        if (key.Length != 32) throw new ArgumentException("key must be 32 bytes", nameof(key));
        if (value.Length != 32) throw new ArgumentException("value must be 32 bytes", nameof(value));
#endif
        byte[] stem = key[..31];
        bool present = _leafUpdateCache.TryGetValue(stem, out LeafUpdateDelta leafUpdateDelta);
        if(!present) leafUpdateDelta = new LeafUpdateDelta();
        leafUpdateDelta.UpdateDelta(UpdateLeafAndGetDelta(key, value), key[31]);
        _leafUpdateCache[stem] = leafUpdateDelta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Banderwagon UpdateLeafAndGetDelta(byte[] key, byte[] value)
    {
        _treeStore.GetLeaf(key, out byte[]? oldValue);
        Banderwagon leafDeltaCommitment = GetLeafDelta(oldValue, value, key[31]);
        _treeStore.SetLeaf(key, value);
        return leafDeltaCommitment;
    }

    private static Banderwagon GetLeafDelta(byte[]? oldValue, byte[] newValue, byte index)
    {

#if DEBUG
        if (oldValue is not null && oldValue.Length != 32) throw new ArgumentException("oldValue must be null or 32 bytes", nameof(oldValue));
        if (newValue.Length != 32) throw new ArgumentException("newValue must be 32 bytes", nameof(newValue));
#endif

        (FrE newValLow, FrE newValHigh) = VerkleUtils.BreakValueInLowHigh(newValue);
        (FrE oldValLow, FrE oldValHigh) = VerkleUtils.BreakValueInLowHigh(oldValue);

        int posMod128 = index % 128;
        int lowIndex = 2 * posMod128;
        int highIndex = lowIndex + 1;

        Banderwagon deltaLow = Utils.Committer.ScalarMul(newValLow - oldValLow, lowIndex);
        Banderwagon deltaHigh = Committer.ScalarMul(newValHigh - oldValHigh, highIndex);
        return deltaLow + deltaHigh;
    }

    public void Commit()
    {
        foreach (KeyValuePair<byte[], LeafUpdateDelta> leafDelta in _leafUpdateCache)
        {
            UpdateTreeCommitments(leafDelta.Key, leafDelta.Value);
        }
        _leafUpdateCache.Clear();
        _isDirty = false;
    }

    public void CommitTree(long blockNumber)
    {
        throw new NotImplementedException();
    }
    public void Accept(ITreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null)
    {
        throw new NotImplementedException();
    }
    public void Accept(IVerkleTreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null)
    {
        throw new NotImplementedException();
    }

    private void UpdateTreeCommitments(Span<byte> stem, LeafUpdateDelta leafUpdateDelta)
    {
        // calculate this by update the leafs and calculating the delta - simple enough
        TraverseContext context = new TraverseContext(stem, leafUpdateDelta);
        Banderwagon rootDelta = TraverseBranch(context);
        UpdateRootNode(rootDelta);
    }

    private void UpdateRootNode(Banderwagon rootDelta)
    {
        if(!_treeStore.GetBranch(Array.Empty<byte>(), out InternalNode? root)) throw new ArgumentException();
        root!._internalCommitment.AddPoint(rootDelta);
        _treeStore.SetBranch(Array.Empty<byte>(), root);
    }

    private ref struct TraverseContext
    {
        public LeafUpdateDelta LeafUpdateDelta { get; }
        public Span<byte> Stem { get; }
        public int CurrentIndex { get; set; }

        public TraverseContext(Span<byte> stem, LeafUpdateDelta delta)
        {
            Stem = stem;
            CurrentIndex = 0;
            LeafUpdateDelta = delta;
        }
    }

    private Banderwagon TraverseBranch(TraverseContext traverseContext)
    {
        byte childIndex = traverseContext.Stem[traverseContext.CurrentIndex];
        byte[] absolutePath = traverseContext.Stem[..(traverseContext.CurrentIndex + 1)].ToArray();

        InternalNode? child = GetBranchNode(absolutePath);
        if (child is null)
        {
            // 1. create new suffix node
            // 2. update the C1 or C2 - we already know the leafDelta - traverseContext.LeafUpdateDelta
            // 3. update ExtensionCommitment
            // 4. get the delta for commitment - ExtensionCommitment - 0;
            (FrE deltaHash, Commitment? suffixCommitment) = UpdateSuffixNode(traverseContext.Stem.ToArray(), traverseContext.LeafUpdateDelta, true);

            // 1. Add internal.stem node
            // 2. return delta from ExtensionCommitment
            _treeStore.SetBranch(absolutePath, new StemNode(traverseContext.Stem.ToArray(), suffixCommitment!));
            return Committer.ScalarMul(deltaHash, childIndex);
        }

        if (child.IsBranchNode)
        {
            traverseContext.CurrentIndex += 1;
            Banderwagon branchDeltaHash = TraverseBranch(traverseContext);
            traverseContext.CurrentIndex -= 1;
            FrE deltaHash = child.UpdateCommitment(branchDeltaHash);
            _treeStore.SetBranch(absolutePath, child);
            return Committer.ScalarMul(deltaHash, childIndex);
        }

        traverseContext.CurrentIndex += 1;
        (Banderwagon stemDeltaHash, bool changeStemToBranch) = TraverseStem(child, traverseContext);
        traverseContext.CurrentIndex -= 1;
        if (changeStemToBranch)
        {
            BranchNode newChild = new BranchNode();
            newChild._internalCommitment.AddPoint(child._internalCommitment.Point);
            // since this is a new child, this would be just the parentDeltaHash.PointToField
            // now since there was a node before and that value is deleted - we need to subtract
            // that from the delta as well
            FrE deltaHash = newChild.UpdateCommitment(stemDeltaHash);
            _treeStore.SetBranch(absolutePath, newChild);
            return Committer.ScalarMul(deltaHash, childIndex);
        }
        // in case of stem, no need to update the child commitment - because this commitment is the suffix commitment
        // pass on the update to upper level
        return stemDeltaHash;
    }

    private (Banderwagon, bool) TraverseStem(InternalNode node, TraverseContext traverseContext)
    {
        Debug.Assert(node.IsStem);

        (List<byte> sharedPath, byte? pathDiffIndexOld, byte? pathDiffIndexNew) =
            VerkleUtils.GetPathDifference(node.Stem, traverseContext.Stem.ToArray());

        if (sharedPath.Count != 31)
        {
            int relativePathLength = sharedPath.Count - traverseContext.CurrentIndex;
            // byte[] relativeSharedPath = sharedPath.ToArray()[traverseContext.CurrentIndex..].ToArray();
            byte oldLeafIndex = pathDiffIndexOld ?? throw new ArgumentException();
            byte newLeafIndex = pathDiffIndexNew ?? throw new ArgumentException();
            // node share a path but not the complete stem.

            // the internal node will be denoted by their sharedPath
            // 1. create SuffixNode for the traverseContext.Key - get the delta of the commitment
            // 2. set this suffix as child node of the BranchNode - get the commitment point
            // 3. set the existing suffix as the child - get the commitment point
            // 4. update the internal node with the two commitment points
            (FrE deltaHash, Commitment? suffixCommitment) = UpdateSuffixNode(traverseContext.Stem.ToArray(), traverseContext.LeafUpdateDelta, true);

            // creating the stem node for the new suffix node
            byte[] branchKey = new byte[sharedPath.Count + 1];
            sharedPath.CopyTo(branchKey);
            branchKey[^1] = newLeafIndex;
            _treeStore.SetBranch(branchKey, new StemNode(traverseContext.Stem.ToArray(), suffixCommitment!));
            Banderwagon newSuffixCommitmentDelta = Committer.ScalarMul(deltaHash, newLeafIndex);


            // instead on declaring new node here - use the node that is input in the function
            branchKey[^1] = oldLeafIndex;
            if (!_treeStore.GetStem(node.Stem, out SuffixTree? oldSuffixNode)) throw new ArgumentException();
            _treeStore.SetBranch(branchKey, new StemNode(node.Stem, oldSuffixNode!.ExtensionCommitment));

            Banderwagon oldSuffixCommitmentDelta =
                Committer.ScalarMul(oldSuffixNode.ExtensionCommitment.PointAsField, oldLeafIndex);

            Banderwagon deltaCommitment = oldSuffixCommitmentDelta + newSuffixCommitmentDelta;

            Banderwagon internalCommitment = FillSpaceWithInternalBranchNodes(sharedPath.ToArray(), relativePathLength, deltaCommitment);

            return (internalCommitment - oldSuffixNode.ExtensionCommitment.Point, true);
        }

        (FrE deltaFr, Commitment updatedSuffixCommitment) = UpdateSuffixNode(traverseContext.Stem.ToArray(), traverseContext.LeafUpdateDelta);
        _treeStore.SetBranch(traverseContext.Stem[..traverseContext.CurrentIndex].ToArray(), new StemNode(node.Stem, updatedSuffixCommitment));
        return (Committer.ScalarMul(deltaFr, traverseContext.Stem[traverseContext.CurrentIndex - 1]), false);
    }

    private InternalNode? GetBranchNode(byte[] pathWithIndex)
    {
        _treeStore.GetBranch(pathWithIndex, out InternalNode? node);
        return node;
    }

    private (FrE, Commitment) UpdateSuffixNode(byte[] stemKey, LeafUpdateDelta leafUpdateDelta, bool insertNew = false)
    {
        SuffixTree? suffixNode;
        if (insertNew) suffixNode = new SuffixTree(stemKey);
        else _treeStore.GetStem(stemKey, out suffixNode);
        ArgumentNullException.ThrowIfNull(suffixNode);

        FrE deltaFr = suffixNode.UpdateCommitment(leafUpdateDelta);
        _treeStore.SetStem(stemKey, suffixNode);
        // add the init commitment, because while calculating diff, we subtract the initCommitment in new nodes.
        return insertNew ? (deltaFr + suffixNode.InitCommitmentHash, suffixNode.ExtensionCommitment.Dup()) : (deltaFr, suffixNode.ExtensionCommitment.Dup());
    }

    private Banderwagon FillSpaceWithInternalBranchNodes(byte[] path, int length, Banderwagon deltaPoint)
    {
        for (int i = 0; i < length; i++)
        {
            BranchNode newInternalNode = new BranchNode();
            FrE upwardsDelta = newInternalNode.UpdateCommitment(deltaPoint);
            _treeStore.SetBranch(path[..^i], newInternalNode);
            deltaPoint = Committer.ScalarMul(upwardsDelta, path[path.Length - i - 1]);
        }

        return deltaPoint;
    }
}
