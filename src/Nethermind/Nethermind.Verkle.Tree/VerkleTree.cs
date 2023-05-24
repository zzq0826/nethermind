// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Proofs;
using Committer = Nethermind.Verkle.Tree.Utils.Committer;
using LeafUpdateDelta = Nethermind.Verkle.Tree.Utils.LeafUpdateDelta;

namespace Nethermind.Verkle.Tree;

public partial class VerkleTree: IVerkleTree
{
    private readonly IVerkleStore _verkleStateStore;

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
            MoveToStateRoot(value);
        }
    }

    private bool _isDirty;

    private readonly Dictionary<byte[], LeafUpdateDelta> _leafUpdateCache;

    public VerkleTree(IDbProvider dbProvider)
    {
        VerkleStateStore verkleStateStore = new VerkleStateStore(dbProvider);
        _verkleStateStore = new VerkleTrieStore(verkleStateStore);
        _leafUpdateCache = new Dictionary<byte[], LeafUpdateDelta>(Bytes.EqualityComparer);
        _stateRoot = _verkleStateStore.RootHash;
        ProofBranchPolynomialCache = new Dictionary<byte[], FrE[]>(Bytes.EqualityComparer);
        ProofStemPolynomialCache = new Dictionary<byte[], SuffixPoly>(Bytes.EqualityComparer);
    }

    protected VerkleTree(IVerkleStore verkleStateStore)
    {
        _verkleStateStore = verkleStateStore;
        _leafUpdateCache = new ();
        _stateRoot = _verkleStateStore.RootHash;
        ProofBranchPolynomialCache = new Dictionary<byte[], FrE[]>(Bytes.EqualityComparer);
        ProofStemPolynomialCache = new Dictionary<byte[], SuffixPoly>(Bytes.EqualityComparer);
    }

    public bool MoveToStateRoot(byte[] stateRoot)
    {
        try
        {
            _verkleStateStore.MoveToStateRoot(stateRoot);
            return true;
        }
        catch (Exception)
        {
            return false;
        }

    }

    public byte[]? Get(byte[] key)
    {
#if DEBUG
        if (key.Length != 32) throw new ArgumentException("key must be 32 bytes", nameof(key));
#endif
        return _verkleStateStore.GetLeaf(key);
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

    public void InsertStemBatch(Span<byte> stem, IEnumerable<(byte, byte[])> leafIndexValueMap)
    {
        _isDirty = true;
#if DEBUG
        if (stem.Length != 31) throw new ArgumentException("stem must be 31 bytes", nameof(stem));
        Span<byte> keyD = new byte[32];
        foreach (KeyValuePair<byte, byte[]> keyVal in leafIndexValueMap)
        {
            stem.CopyTo(keyD);
            keyD[31] = keyVal.Key;
            Console.WriteLine("KA: " + EnumerableExtensions.ToString(keyD.ToArray()));
            Console.WriteLine("V: " + EnumerableExtensions.ToString(keyVal.Value));
        }
#endif
        bool present = _leafUpdateCache.TryGetValue(stem.ToArray(), out LeafUpdateDelta leafUpdateDelta);
        if(!present) leafUpdateDelta = new LeafUpdateDelta();

        Span<byte> key = new byte[32];
        stem.CopyTo(key);
        foreach ((byte index, byte[] value) in leafIndexValueMap)
        {
            key[31] = index;
            leafUpdateDelta.UpdateDelta(UpdateLeafAndGetDelta(key.ToArray(), value), key[31]);
        }

        _leafUpdateCache[stem.ToArray()] = leafUpdateDelta;
    }

    private Banderwagon UpdateLeafAndGetDelta(byte[] key, byte[] value)
    {
        byte[]? oldValue = _verkleStateStore.GetLeaf(key);
        Banderwagon leafDeltaCommitment = GetLeafDelta(oldValue, value, key[31]);
        _verkleStateStore.SetLeaf(key, value);
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

        Banderwagon deltaLow = Committer.ScalarMul(newValLow - oldValLow, lowIndex);
        Banderwagon deltaHigh = Committer.ScalarMul(newValHigh - oldValHigh, highIndex);
        return deltaLow + deltaHigh;
    }

    public static Banderwagon GetLeafDelta(byte[] newValue, byte index)
    {

#if DEBUG
        if (oldValue is not null && oldValue.Length != 32) throw new ArgumentException("oldValue must be null or 32 bytes", nameof(oldValue));
        if (newValue.Length != 32) throw new ArgumentException("newValue must be 32 bytes", nameof(newValue));
#endif

        (FrE newValLow, FrE newValHigh) = VerkleUtils.BreakValueInLowHigh(newValue);

        int posMod128 = index % 128;
        int lowIndex = 2 * posMod128;
        int highIndex = lowIndex + 1;

        Banderwagon deltaLow = Committer.ScalarMul(newValLow, lowIndex);
        Banderwagon deltaHigh = Committer.ScalarMul(newValHigh, highIndex);
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
        _stateRoot = _verkleStateStore.RootHash;
    }

    public void CommitTree(long blockNumber)
    {
        _verkleStateStore.Flush(blockNumber);
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
        InternalNode root = _verkleStateStore.GetInternalNode(Array.Empty<byte>()) ?? throw new InvalidOperationException("root should be present");
        InternalNode newRoot = root.Clone();
        newRoot.InternalCommitment.AddPoint(rootDelta);
        _verkleStateStore.SetInternalNode(Array.Empty<byte>(), newRoot);
    }

    private Banderwagon TraverseBranch(TraverseContext traverseContext)
    {
        byte childIndex = traverseContext.Stem[traverseContext.CurrentIndex];
        byte[] absolutePath = traverseContext.Stem[..(traverseContext.CurrentIndex + 1)].ToArray();

        InternalNode? child = _verkleStateStore.GetInternalNode(absolutePath);
        if (child is null)
        {
            // 1. create new suffix node
            // 2. update the C1 or C2 - we already know the leafDelta - traverseContext.LeafUpdateDelta
            // 3. update ExtensionCommitment
            // 4. get the delta for commitment - ExtensionCommitment - 0;
            InternalNode stem = new InternalNode(VerkleNodeType.StemNode, traverseContext.Stem.ToArray());
            FrE deltaFr = stem.UpdateCommitment(traverseContext.LeafUpdateDelta);
            FrE deltaHash = deltaFr + stem.InitCommitmentHash!.Value;

            // 1. Add internal.stem node
            // 2. return delta from ExtensionCommitment
            _verkleStateStore.SetInternalNode(absolutePath, stem);
            return Committer.ScalarMul(deltaHash, childIndex);
        }

        if (child.IsBranchNode)
        {
            traverseContext.CurrentIndex += 1;
            Banderwagon branchDeltaHash = TraverseBranch(traverseContext);
            traverseContext.CurrentIndex -= 1;
            FrE deltaHash = child.UpdateCommitment(branchDeltaHash);
            _verkleStateStore.SetInternalNode(absolutePath, child);
            return Committer.ScalarMul(deltaHash, childIndex);
        }

        traverseContext.CurrentIndex += 1;
        (Banderwagon stemDeltaHash, bool changeStemToBranch) = TraverseStem(child, traverseContext);
        traverseContext.CurrentIndex -= 1;
        if (changeStemToBranch)
        {
            InternalNode newChild = new InternalNode(VerkleNodeType.BranchNode);
            newChild.InternalCommitment.AddPoint(child.InternalCommitment.Point);
            // since this is a new child, this would be just the parentDeltaHash.PointToField
            // now since there was a node before and that value is deleted - we need to subtract
            // that from the delta as well
            FrE deltaHash = newChild.UpdateCommitment(stemDeltaHash);
            _verkleStateStore.SetInternalNode(absolutePath, newChild);
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
            VerkleUtils.GetPathDifference(node.Stem!, traverseContext.Stem.ToArray());

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
            InternalNode newStem = new InternalNode(VerkleNodeType.StemNode, traverseContext.Stem.ToArray());
            FrE deltaFrNewStem = newStem.UpdateCommitment(traverseContext.LeafUpdateDelta);
            FrE deltaHashNewStem = deltaFrNewStem + newStem.InitCommitmentHash!.Value;

            // creating the stem node for the new suffix node
            byte[] stemKey = new byte[sharedPath.Count + 1];
            sharedPath.CopyTo(stemKey);
            stemKey[^1] = newLeafIndex;
            _verkleStateStore.SetInternalNode(stemKey, newStem);
            Banderwagon newSuffixCommitmentDelta = Committer.ScalarMul(deltaHashNewStem, newLeafIndex);

            stemKey = new byte[sharedPath.Count + 1];
            sharedPath.CopyTo(stemKey);
            stemKey[^1] = oldLeafIndex;
            _verkleStateStore.SetInternalNode(stemKey, node);

            Banderwagon oldSuffixCommitmentDelta =
                Committer.ScalarMul(node.InternalCommitment.PointAsField, oldLeafIndex);

            Banderwagon deltaCommitment = oldSuffixCommitmentDelta + newSuffixCommitmentDelta;

            Banderwagon internalCommitment = FillSpaceWithBranchNodes(sharedPath.ToArray(), relativePathLength, deltaCommitment);

            return (internalCommitment - node.InternalCommitment.Point, true);
        }

        InternalNode updatedStemNode = node.Clone();
        FrE deltaFr = updatedStemNode.UpdateCommitment(traverseContext.LeafUpdateDelta);
        _verkleStateStore.SetInternalNode(traverseContext.Stem[..traverseContext.CurrentIndex].ToArray(), updatedStemNode);
        return (Committer.ScalarMul(deltaFr, traverseContext.Stem[traverseContext.CurrentIndex - 1]), false);
    }

    private Banderwagon FillSpaceWithBranchNodes(byte[] path, int length, Banderwagon deltaPoint)
    {
        for (int i = 0; i < length; i++)
        {
            InternalNode newInternalNode = new InternalNode(VerkleNodeType.BranchNode);
            FrE upwardsDelta = newInternalNode.UpdateCommitment(deltaPoint);
            _verkleStateStore.SetInternalNode(path[..^i], newInternalNode);
            deltaPoint = Committer.ScalarMul(upwardsDelta, path[path.Length - i - 1]);
        }

        return deltaPoint;
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
}
