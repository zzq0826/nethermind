// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Collections;
using Nethermind.Core.Extensions;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Proofs;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;
using Committer = Nethermind.Core.Verkle.Committer;
using LeafUpdateDelta = Nethermind.Verkle.Tree.Utils.LeafUpdateDelta;

namespace Nethermind.Verkle.Tree;

public partial class VerkleTree: IVerkleTree
{
    private static readonly byte[] _rootKey = Array.Empty<byte>();
    private readonly ILogger _logger;

    // get and update the state root of the tree
    // _isDirty - to check if tree is in between insertion and commitment
    private bool _isDirty;
    private Pedersen _stateRoot;
    public Pedersen StateRoot
    {
        get => GetStateRoot();
        set => MoveToStateRoot(value);
    }

    // the store that is responsible to store the tree in a key-value store
    public readonly IVerkleStore _verkleStateStore;

    // cache to maintain recently used or inserted nodes of the tree - should be consistent
    private VerkleMemoryDb _treeCache;

    // aggregate the stem commitments here and then update the entire tree when Commit() is called
    private readonly SpanDictionary<byte, LeafUpdateDelta> _leafUpdateCache;

    public VerkleTree(IDbProvider dbProvider, ILogManager? logManager)
    {
        _logger = logManager?.GetClassLogger<VerkleTree>() ?? throw new ArgumentNullException(nameof(logManager));
        _treeCache = new VerkleMemoryDb();
        _verkleStateStore = new VerkleStateStore(dbProvider, logManager);
        _leafUpdateCache = new SpanDictionary<byte, LeafUpdateDelta>(Bytes.SpanEqualityComparer);
        _stateRoot = _verkleStateStore.StateRoot;
        ProofBranchPolynomialCache = new Dictionary<byte[], FrE[]>(Bytes.EqualityComparer);
        ProofStemPolynomialCache = new Dictionary<Stem, SuffixPoly>();
    }

    public VerkleTree(IVerkleStore verkleStateStore, ILogManager? logManager)
    {
        _logger = logManager?.GetClassLogger<VerkleTree>() ?? throw new ArgumentNullException(nameof(logManager));
        _treeCache = new VerkleMemoryDb();
        _verkleStateStore = verkleStateStore;
        _leafUpdateCache = new SpanDictionary<byte, LeafUpdateDelta>(Bytes.SpanEqualityComparer);
        _stateRoot = _verkleStateStore.StateRoot;
        ProofBranchPolynomialCache = new Dictionary<byte[], FrE[]>(Bytes.EqualityComparer);
        ProofStemPolynomialCache = new Dictionary<Stem, SuffixPoly>();
    }

    private Pedersen GetStateRoot()
    {
        InternalNode rootNode = GetInternalNode(Array.Empty<byte>()) ?? throw new InvalidOperationException();
        return new Pedersen(rootNode.InternalCommitment.Point.ToBytes().ToArray());
    }

    public bool MoveToStateRoot(Pedersen stateRoot)
    {
        try
        {
            if (_logger.IsTrace) _logger.Trace($"MoveToStateRoot: isDirty: {_isDirty} from: {_stateRoot} to: {stateRoot}");
            _verkleStateStore.MoveToStateRoot(stateRoot);
            return true;
        }
        catch (Exception e)
        {
            if (_logger.IsDebug)
                _logger.Error($"MoveToStateRoot: failed isDirty: {_isDirty} from: {_stateRoot} to: {stateRoot}", e);
            return false;
        }
    }

    public byte[]? Get(Pedersen key)
    {
        _treeCache.GetLeaf(key.Bytes, out byte[]? value);
        value ??= _verkleStateStore.GetLeaf(key.Bytes);
        return value;
    }

    private void SetLeafCache(Pedersen key, byte[]? value)
    {
        _treeCache.SetLeaf(key.BytesAsSpan, value);
    }

    public void Insert(Pedersen key, ReadOnlySpan<byte> value)
    {
        _isDirty = true;
#if DEBUG
        if (value.Length != 32) throw new ArgumentException("value must be 32 bytes", nameof(value));
        SimpleConsoleLogger.Instance.Info($"K:{key.Bytes.ToHexString()} V:{value.ToHexString()}");
#endif
        ReadOnlySpan<byte> stem = key.StemAsSpan;
        bool present = _leafUpdateCache.TryGetValue(stem, out LeafUpdateDelta leafUpdateDelta);
        if (!present) leafUpdateDelta = new LeafUpdateDelta();
        leafUpdateDelta.UpdateDelta(UpdateLeafAndGetDelta(key, value.ToArray()), key.SuffixByte);
        _leafUpdateCache[stem] = leafUpdateDelta;
    }

    public void InsertStemBatch(ReadOnlySpan<byte> stem, IEnumerable<(byte, byte[])> leafIndexValueMap)
    {
        _isDirty = true;
#if DEBUG
        if (stem.Length != 31) throw new ArgumentException("stem must be 31 bytes", nameof(stem));
#endif
        bool present = _leafUpdateCache.TryGetValue(stem, out LeafUpdateDelta leafUpdateDelta);
        if(!present) leafUpdateDelta = new LeafUpdateDelta();

        Span<byte> key = new byte[32];
        stem.CopyTo(key);
        foreach ((byte index, byte[] value) in leafIndexValueMap)
        {
            key[31] = index;
#if DEBUG
             SimpleConsoleLogger.Instance.Info($"K:{key.ToHexString()} V:{value.ToHexString()}");
#endif
            leafUpdateDelta.UpdateDelta(UpdateLeafAndGetDelta(new Pedersen(key.ToArray()), value), key[31]);
        }

        _leafUpdateCache[stem.ToArray()] = leafUpdateDelta;
    }

    public void InsertStemBatch(ReadOnlySpan<byte> stem, IEnumerable<LeafInSubTree> leafIndexValueMap)
    {
        _isDirty = true;
#if DEBUG
        if (stem.Length != 31) throw new ArgumentException("stem must be 31 bytes", nameof(stem));
#endif
        bool present = _leafUpdateCache.TryGetValue(stem, out LeafUpdateDelta leafUpdateDelta);
        if(!present) leafUpdateDelta = new LeafUpdateDelta();

        Span<byte> key = new byte[32];
        stem.CopyTo(key);
        foreach (LeafInSubTree leaf in leafIndexValueMap)
        {
            key[31] = leaf.SuffixByte;
#if DEBUG
            SimpleConsoleLogger.Instance.Info($"K:{key.ToHexString()} V:{leaf.Leaf?.ToHexString()}");
#endif
            leafUpdateDelta.UpdateDelta(UpdateLeafAndGetDelta(new Pedersen(key.ToArray()), leaf.Leaf), key[31]);
        }

        _leafUpdateCache[stem.ToArray()] = leafUpdateDelta;
    }

    public void InsertStemBatch(in Stem stem, IEnumerable<LeafInSubTree> leafIndexValueMap)
    {
        InsertStemBatch(stem.BytesAsSpan, leafIndexValueMap);
    }

    private void InsertStemBatchStateless(in Stem stem, IEnumerable<LeafInSubTree> leafIndexValueMap)
    {
        InsertStemBatchStateless(stem.BytesAsSpan, leafIndexValueMap);
    }

    private void InsertStemBatchStateless(ReadOnlySpan<byte> stem, IEnumerable<LeafInSubTree> leafIndexValueMap)
    {
        Span<byte> key = new byte[32];
        stem.CopyTo(key);
        foreach (LeafInSubTree leaf in leafIndexValueMap)
        {
            key[31] = leaf.SuffixByte;
#if DEBUG
            SimpleConsoleLogger.Instance.Info($"K:{key.ToHexString()} V:{leaf.Leaf?.ToHexString()}");
#endif
            SetLeafCache(key.ToArray(), leaf.Leaf);
        }
    }

    private Banderwagon UpdateLeafAndGetDelta(Pedersen key, byte[] value)
    {
        byte[]? oldValue = Get(key);
        Banderwagon leafDeltaCommitment = GetLeafDelta(oldValue, value, key.SuffixByte);
        SetLeafCache(key, value);
        return leafDeltaCommitment;
    }

    private static Banderwagon GetLeafDelta(byte[]? oldValue, byte[] newValue, byte index)
    {

#if DEBUG
        if (oldValue is not null && oldValue.Length != 32) throw new ArgumentException("oldValue must be null or 32 bytes", nameof(oldValue));
        if (newValue.Length != 32) throw new ArgumentException("newValue must be 32 bytes", nameof(newValue));
#endif

        // break the values to calculate the commitments for the leaf
        (FrE newValLow, FrE newValHigh) = VerkleUtils.BreakValueInLowHigh(newValue);
        (FrE oldValLow, FrE oldValHigh) = VerkleUtils.BreakValueInLowHigh(oldValue);

        int posMod128 = index % 128;
        int lowIndex = 2 * posMod128;
        int highIndex = lowIndex + 1;

        Banderwagon deltaLow = Committer.ScalarMul(newValLow - oldValLow, lowIndex);
        Banderwagon deltaHigh = Committer.ScalarMul(newValHigh - oldValHigh, highIndex);
        return deltaLow + deltaHigh;
    }

    private static Banderwagon GetLeafDelta(byte[] newValue, byte index)
    {

#if DEBUG
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

    public void Commit(bool forSync = false)
    {
        if (_logger.IsDebug) _logger.Debug($"Commiting: number of subTrees {_leafUpdateCache.Count}");
        foreach (KeyValuePair<byte[], LeafUpdateDelta> leafDelta in _leafUpdateCache)
        {
            if (_logger.IsTrace)
                _logger.Trace(
                    $"Commit: stem:{leafDelta.Key.ToHexString()} deltaCommitment:C1:{leafDelta.Value.DeltaC1?.ToBytes().ToHexString()} C2{leafDelta.Value.DeltaC2?.ToBytes().ToHexString()}");
            UpdateTreeCommitments(leafDelta.Key, leafDelta.Value, forSync);
        }
        _leafUpdateCache.Clear();
        _isDirty = false;
        _stateRoot = GetStateRoot();
    }

    public void CommitTree(long blockNumber)
    {
        _isDirty = false;
        _verkleStateStore.Flush(blockNumber, _treeCache);
        _treeCache = new VerkleMemoryDb();
        _stateRoot = _verkleStateStore.StateRoot;
    }

    private void UpdateRootNode(Banderwagon rootDelta)
    {
        InternalNode root = GetInternalNode(_rootKey) ?? throw new InvalidOperationException("root should be present");
        InternalNode newRoot = root.Clone();
        newRoot.InternalCommitment.AddPoint(rootDelta);
        SetInternalNode(_rootKey, newRoot);
    }

    private InternalNode? GetInternalNode(ReadOnlySpan<byte> nodeKey)
    {
        return _treeCache.GetInternalNode(nodeKey, out InternalNode? value)
            ? value
            : _verkleStateStore.GetInternalNode(nodeKey);
    }

    private void SetInternalNode(byte[] nodeKey, InternalNode node, bool replace = true)
    {
        if (replace || !_treeCache.InternalTable.TryGetValue(nodeKey, out InternalNode? prevNode))
        {
            _treeCache.SetInternalNode(nodeKey, node);
        }
        else
        {
            prevNode.C1 ??= node.C1;
            prevNode.C2 ??= node.C2;
            _treeCache.SetInternalNode(nodeKey, prevNode);
        }
    }

    public void Reset()
    {
        _leafUpdateCache.Clear();
        ProofBranchPolynomialCache.Clear();
        ProofStemPolynomialCache.Clear();
        _treeCache = new VerkleMemoryDb();
        _verkleStateStore.Reset();
    }
}
