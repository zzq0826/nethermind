using System.Diagnostics;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.VerkleNodes;
using Nethermind.Verkle.VerkleStateDb;

namespace Nethermind.Verkle;

public class VerkleStateStore : IVerkleStore, ISyncTrieStore
{
    public byte[] RootHash
    {
        get => GetStateRoot();
        set => MoveToStateRoot(value);
    }
    // the blockNumber for with the fullState exists.
    private long FullStateBlock { get; set; }

    // The underlying key value database
    // We try to avoid fetching from this, and we only store at the end of a batch insert
    private IVerkleDb Storage { get; }

    // This stores the key-value pairs that we need to insert into the storage. This is generally
    // used to batch insert changes for each block. This is also used to generate the forwardDiff.
    // This is flushed after every batch insert and cleared.
    private IVerkleMemoryDb Batch { get; set; }

    // This should store the top 3 layers of the trie, since these are the most accessed in
    // the trie on average, thus speeding up some operations. But right now every things is
    // stored in the cache - bad design.
    // TODO: modify the cache to only store the top 3 layers
    private IVerkleMemoryDb Cache { get; }

    // These database stores the forwardDiff and reverseDiff for each block. Diffs are generated when
    // the Flush(long blockNumber) method is called.
    // TODO: add capability to update the diffs instead of overwriting if Flush(long blockNumber) is called multiple times for the same block number
    private DiffLayer ForwardDiff { get; }
    private DiffLayer ReverseDiff { get; }

    private IDb StateRootToBlocks { get; }

    public VerkleStateStore(IDbProvider dbProvider)
    {
        Storage = new VerkleDb(dbProvider);
        Batch = new MemoryStateDb();
        Cache = new MemoryStateDb();
        ForwardDiff = new DiffLayer(dbProvider.ForwardDiff, DiffType.Forward);
        ReverseDiff = new DiffLayer(dbProvider.ReverseDiff, DiffType.Reverse);
        StateRootToBlocks = dbProvider.StateRootToBlocks;
        FullStateBlock = 0;
        InitRootHash();
    }

    public ReadOnlyVerkleStateStore AsReadOnly(IVerkleMemoryDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(this, keyValueStore);
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock < toBlock - move forward in time
    public IVerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock)
    {
        return ForwardDiff.MergeDiffs(fromBlock, toBlock);
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock > toBlock - move back in time
    public IVerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock)
    {
        return ReverseDiff.MergeDiffs(fromBlock, toBlock);
    }
    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    private void InitRootHash()
    {
        if(Batch.GetBranch(Array.Empty<byte>(), out InternalNode? _)) return;
        Batch.SetBranch(Array.Empty<byte>(), new BranchNode());
    }

    public byte[]? GetLeaf(byte[] key)
    {
        if (Cache.GetLeaf(key, out byte[]? value)) return value;
        if (Batch.GetLeaf(key, out value)) return value;
        return Storage.GetLeaf(key, out value) ? value : null;
    }

    public SuffixTree? GetStem(byte[] key)
    {
        if (Cache.GetStem(key, out SuffixTree? value)) return value;
        if (Batch.GetStem(key, out value)) return value;
        return Storage.GetStem(key, out value) ? value : null;
    }

    public InternalNode? GetBranch(byte[] key)
    {
        if (Cache.GetBranch(key, out InternalNode? value)) return value;
        if (Batch.GetBranch(key, out value)) return value;
        return Storage.GetBranch(key, out value) ? value : null;
    }

    public void SetLeaf(byte[] leafKey, byte[] leafValue)
    {
        Cache.SetLeaf(leafKey, leafValue);
        Batch.SetLeaf(leafKey, leafValue);
    }

    public void SetStem(byte[] stemKey, SuffixTree suffixTree)
    {
        Cache.SetStem(stemKey, suffixTree);
        Batch.SetStem(stemKey, suffixTree);
    }

    public void SetBranch(byte[] branchKey, InternalNode internalNodeValue)
    {
        Cache.SetBranch(branchKey, internalNodeValue);
        Batch.SetBranch(branchKey, internalNodeValue);
    }

    // This method is called at the end of each block to flush the batch changes to the storage and generate forward and reverse diffs.
    // this should be called only once per block, right now it does not support multiple calls for the same block number.
    // if called multiple times, the full state would be fine - but it would corrupt the diffs and historical state will be lost
    // TODO: add capability to update the diffs instead of overwriting if Flush(long blockNumber) is called multiple times for the same block number
    public void Flush(long blockNumber)
    {
        // we should not have any null values in the Batch db - because deletion of values from verkle tree is not allowed
        // nullable values are allowed in MemoryStateDb only for reverse diffs.
        MemoryStateDb reverseDiff = new MemoryStateDb();

        foreach (KeyValuePair<byte[], byte[]?> entry in Batch.LeafTable)
        {
            Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (Storage.GetLeaf(entry.Key, out byte[]? node)) reverseDiff.LeafTable[entry.Key] = node;
            else reverseDiff.LeafTable[entry.Key] = null;

            Storage.SetLeaf(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], SuffixTree?> entry in Batch.StemTable)
        {
            Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (Storage.GetStem(entry.Key, out SuffixTree? node)) reverseDiff.StemTable[entry.Key] = node;
            else reverseDiff.StemTable[entry.Key] = null;

            Storage.SetStem(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in Batch.BranchTable)
        {
            Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (Storage.GetBranch(entry.Key, out InternalNode? node)) reverseDiff.BranchTable[entry.Key] = node;
            else reverseDiff.BranchTable[entry.Key] = null;

            Storage.SetBranch(entry.Key, entry.Value);
        }

        ForwardDiff.InsertDiff(blockNumber, Batch);
        ReverseDiff.InsertDiff(blockNumber, reverseDiff);
        FullStateBlock = blockNumber;
        StateRootToBlocks.Set(GetBranch(Array.Empty<byte>())?._internalCommitment.PointAsField.ToBytes().ToArray() ?? throw new InvalidOperationException(), blockNumber.ToBigEndianByteArrayWithoutLeadingZeros());

        Batch = new MemoryStateDb();
    }

    // now the full state back in time by one block.
    public void ReverseState()
    {
        IVerkleMemoryDb reverseDiff = ReverseDiff.FetchDiff(FullStateBlock);

        foreach (KeyValuePair<byte[], byte[]?> entry in reverseDiff.LeafTable)
        {
            reverseDiff.GetLeaf(entry.Key, out byte[]? node);
            if (node is null)
            {
                Cache.RemoveLeaf(entry.Key);
                Storage.RemoveLeaf(entry.Key);
            }
            else
            {
                Cache.SetLeaf(entry.Key, node);
                Storage.SetLeaf(entry.Key, node);
            }
        }

        foreach (KeyValuePair<byte[], SuffixTree?> entry in reverseDiff.StemTable)
        {
            reverseDiff.GetStem(entry.Key, out SuffixTree? node);
            if (node is null)
            {
                Cache.RemoveStem(entry.Key);
                Storage.RemoveStem(entry.Key);
            }
            else
            {
                Cache.SetStem(entry.Key, node);
                Storage.SetStem(entry.Key, node);
            }
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in reverseDiff.BranchTable)
        {
            reverseDiff.GetBranch(entry.Key, out InternalNode? node);
            if (node is null)
            {
                Cache.RemoveBranch(entry.Key);
                Storage.RemoveBranch(entry.Key);
            }
            else
            {
                Cache.SetBranch(entry.Key, node);
                Storage.SetBranch(entry.Key, node);
            }
        }
        FullStateBlock -= 1;
    }

    // use the batch diff to move the full state back in time to access historical state.
    public void ApplyDiffLayer(IVerkleMemoryDb reverseBatch, long fromBlock, long toBlock)
    {
        if (fromBlock != FullStateBlock) throw new ArgumentException($"Cannot apply diff FullStateBlock:{FullStateBlock}!=fromBlock:{fromBlock}", nameof(fromBlock));

        MemoryStateDb reverseDiff = (MemoryStateDb)reverseBatch;

        foreach (KeyValuePair<byte[], byte[]?> entry in reverseDiff.LeafTable)
        {
            reverseDiff.GetLeaf(entry.Key, out byte[]? node);
            if (node is null)
            {
                Cache.RemoveLeaf(entry.Key);
                Storage.RemoveLeaf(entry.Key);
            }
            else
            {
                Cache.SetLeaf(entry.Key, node);
                Storage.SetLeaf(entry.Key, node);
            }
        }

        foreach (KeyValuePair<byte[], SuffixTree?> entry in reverseDiff.StemTable)
        {
            reverseDiff.GetStem(entry.Key, out SuffixTree? node);
            if (node is null)
            {
                Cache.RemoveStem(entry.Key);
                Storage.RemoveStem(entry.Key);
            }
            else
            {
                Cache.SetStem(entry.Key, node);
                Storage.SetStem(entry.Key, node);
            }
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in reverseDiff.BranchTable)
        {
            reverseDiff.GetBranch(entry.Key, out InternalNode? node);
            if (node is null)
            {
                Cache.RemoveBranch(entry.Key);
                Storage.RemoveBranch(entry.Key);
            }
            else
            {
                Cache.SetBranch(entry.Key, node);
                Storage.SetBranch(entry.Key, node);
            }
        }
        FullStateBlock = toBlock;
    }
    public bool IsFullySynced(Keccak stateRoot)
    {
        return false;
    }

    public byte[] GetStateRoot()
    {
        return GetBranch(Array.Empty<byte>())?._internalCommitment.PointAsField.ToBytes().ToArray() ?? throw new InvalidOperationException();
    }

    public void MoveToStateRoot(byte[] stateRoot)
    {
        byte[] currentRoot = GetBranch(Array.Empty<byte>())?._internalCommitment.PointAsField.ToBytes().ToArray() ?? throw new InvalidOperationException();

        if (currentRoot.SequenceEqual(stateRoot)) return;
        if (Keccak.EmptyTreeHash.Equals(stateRoot)) return;

        byte[]? fromBlockBytes = StateRootToBlocks[currentRoot];
        byte[]? toBlockBytes = StateRootToBlocks[stateRoot];
        if (fromBlockBytes is null) return;
        if (toBlockBytes is null) return;

        long fromBlock = fromBlockBytes.ToLongFromBigEndianByteArrayWithoutLeadingZeros();
        long toBlock = toBlockBytes.ToLongFromBigEndianByteArrayWithoutLeadingZeros();

        ApplyDiffLayer(fromBlock > toBlock ? ReverseDiff.MergeDiffs(fromBlock, toBlock) : ForwardDiff.MergeDiffs(fromBlock, toBlock), fromBlock, toBlock);

        Debug.Assert(GetStateRoot().Equals(stateRoot));
    }
}

public interface IVerkleStore: IStoreWithReorgBoundary
{
    byte[]? GetLeaf(byte[] key);
    SuffixTree? GetStem(byte[] key);
    InternalNode? GetBranch(byte[] key);
    void SetLeaf(byte[] leafKey, byte[] leafValue);
    void SetStem(byte[] stemKey, SuffixTree suffixTree);
    void SetBranch(byte[] branchKey, InternalNode internalNodeValue);
    void Flush(long blockNumber);
    void ReverseState();
    void ApplyDiffLayer(IVerkleMemoryDb reverseBatch, long fromBlock, long toBlock);

    byte[] GetStateRoot();
    void MoveToStateRoot(byte[] stateRoot);

    public ReadOnlyVerkleStateStore AsReadOnly(IVerkleMemoryDb keyValueStore);

    public IVerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock);

    public IVerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock);

}
