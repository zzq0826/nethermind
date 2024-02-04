// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Resettables;
using Nethermind.Logging;
using Nethermind.State.Tracing;
using Nethermind.Trie.Pruning;

namespace Nethermind.State
{
    /// <summary>
    /// Manages persistent storage allowing for snapshotting and restoring
    /// Persists data to ITrieStore
    /// </summary>
    internal class PersistentStorageProvider : PartialStorageProviderBase
    {
        private readonly ITrieStore _trieStore;
        private readonly StateProvider _stateProvider;
        private readonly ILogManager? _logManager;
        internal readonly IStorageTreeFactory _storageTreeFactory;
        private readonly StorageCache _storages;
        private readonly LruCache<StorageCell, byte[]> _cache = new(6000, "");

        /// <summary>
        /// EIP-1283
        /// </summary>
        private readonly ResettableDictionary<StorageCell, byte[]> _originalValues = new();

        private readonly ResettableHashSet<StorageCell> _committedThisRound = new();

        public PersistentStorageProvider(ITrieStore? trieStore, StateProvider? stateProvider, ILogManager? logManager, IStorageTreeFactory? storageTreeFactory = null)
            : base(logManager)
        {
            _trieStore = trieStore ?? throw new ArgumentNullException(nameof(trieStore));
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _storageTreeFactory = storageTreeFactory ?? new StorageTreeFactory();
            _storages = new(this, _storageTreeFactory);
        }

        public Hash256 StateRoot { get; set; } = null!;

        /// <summary>
        /// Reset the storage state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            //_storages.Reset();
            _originalValues.Clear();
            _committedThisRound.Clear();
        }

        /// <summary>
        /// Get the current value at the specified location
        /// </summary>
        /// <param name="storageCell">Storage location</param>
        /// <returns>Value at location</returns>
        protected override ReadOnlySpan<byte> GetCurrentValue(in StorageCell storageCell) =>
            TryGetCachedValue(storageCell, out byte[]? bytes) ? bytes! : LoadFromTree(storageCell);

        /// <summary>
        /// Return the original persistent storage value from the storage cell
        /// </summary>
        /// <param name="storageCell"></param>
        /// <returns></returns>
        public byte[] GetOriginal(in StorageCell storageCell)
        {
            if (!_originalValues.TryGetValue(storageCell, out var value))
            {
                throw new InvalidOperationException("Get original should only be called after get within the same caching round");
            }

            if (_transactionChangesSnapshots.TryPeek(out int snapshot))
            {
                if (_intraBlockCache.TryGetValue(storageCell, out StackList<int> stack))
                {
                    if (stack.TryGetSearchedItem(snapshot, out int lastChangeIndexBeforeOriginalSnapshot))
                    {
                        return _changes[lastChangeIndexBeforeOriginalSnapshot]!.Value;
                    }
                }
            }

            return value;
        }


        /// <summary>
        /// Called by Commit
        /// Used for persistent storage specific logic
        /// </summary>
        /// <param name="tracer">Storage tracer</param>
        protected override void CommitCore(IStorageTracer tracer)
        {
            if (_logger.IsTrace) _logger.Trace("Committing storage changes");

            if (_changes[_currentPosition] is null)
            {
                throw new InvalidOperationException($"Change at current position {_currentPosition} was null when commiting {nameof(PartialStorageProviderBase)}");
            }

            if (_changes[_currentPosition + 1] is not null)
            {
                throw new InvalidOperationException($"Change after current position ({_currentPosition} + 1) was not null when commiting {nameof(PartialStorageProviderBase)}");
            }

            HashSet<Address> toUpdateRoots = new();

            bool isTracing = tracer.IsTracingStorage;
            Dictionary<StorageCell, ChangeTrace>? trace = null;
            if (isTracing)
            {
                trace = new Dictionary<StorageCell, ChangeTrace>();
            }

            for (int i = 0; i <= _currentPosition; i++)
            {
                Change change = _changes[_currentPosition - i];
                if (!isTracing && change!.ChangeType == ChangeType.JustCache)
                {
                    continue;
                }

                if (_committedThisRound.Contains(change!.StorageCell))
                {
                    if (isTracing && change.ChangeType == ChangeType.JustCache)
                    {
                        trace![change.StorageCell] = new ChangeTrace(change.Value, trace[change.StorageCell].After);
                    }

                    continue;
                }

                if (isTracing && change.ChangeType == ChangeType.JustCache)
                {
                    tracer!.ReportStorageRead(change.StorageCell);
                }

                _committedThisRound.Add(change.StorageCell);

                if (change.ChangeType == ChangeType.Destroy)
                {
                    continue;
                }

                int forAssertion = _intraBlockCache[change.StorageCell].Pop();
                if (forAssertion != _currentPosition - i)
                {
                    throw new InvalidOperationException($"Expected checked value {forAssertion} to be equal to {_currentPosition} - {i}");
                }

                switch (change.ChangeType)
                {
                    case ChangeType.Destroy:
                        break;
                    case ChangeType.JustCache:
                        break;
                    case ChangeType.Update:
                        if (_logger.IsTrace)
                        {
                            _logger.Trace($"  Update {change.StorageCell.Address}_{change.StorageCell.Index} V = {change.Value.ToHexString(true)}");
                        }

                        SaveToTree(in change.StorageCell, change.Value);
                        toUpdateRoots.Add(change.StorageCell.Address);
                        if (isTracing)
                        {
                            trace![change.StorageCell] = new ChangeTrace(change.Value);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // TODO: it seems that we are unnecessarily recalculating root hashes all the time in storage?
            foreach (Address address in toUpdateRoots)
            {
                // since the accounts could be empty accounts that are removing (EIP-158)
                if (_stateProvider.AccountExists(address))
                {
                    Hash256 root = RecalculateRootHash(address);

                    // _logger.Warn($"Recalculating storage root {address}->{root} ({toUpdateRoots.Count})");
                    _stateProvider.UpdateStorageRoot(address, root);
                }
            }

            base.CommitCore(tracer);
            _originalValues.Reset();
            _committedThisRound.Reset();

            if (isTracing)
            {
                ReportChanges(tracer!, trace!);
            }
        }

        /// <summary>
        /// Commit persistent storage trees
        /// </summary>
        /// <param name="blockNumber">Current block number</param>
        public void CommitBlock(long blockNumber)
        {
            // _logger.Warn($"Storage block commit {blockNumber}");
            foreach (KeyValuePair<Address, StorageTree> storage in _storages)
            {
                storage.Value.Commit(blockNumber);
            }
            // TODO: maybe I could update storage roots only now?
            
            _storages.Reset();
            // only needed here as there is no control over cached storage size otherwise
            _cache.Clear();
        }

        private StorageTree GetOrCreateStorage(Address address)
        {
            return _storages.Get(address);
        }

        private ReadOnlySpan<byte> LoadFromTree(in StorageCell storageCell)
        {
            byte[] value;
            //if (!storageCell.IsHash && _cache.TryGet(storageCell, out byte[] value))
            //{
            //    PushToRegistryOnly(storageCell, value);
            //    return value;
            //}

            StorageTree tree = GetOrCreateStorage(storageCell.Address);

            Db.Metrics.StorageTreeReads++;

            if (!storageCell.IsHash)
            {
                value = tree.Get(storageCell.Index);
                PushToRegistryOnly(storageCell, value);
                return value;
            }

            return tree.Get(storageCell.Hash.Bytes);
        }

        private void SaveToTree(in StorageCell storageCell, byte[] value)
        {
            StorageTree tree = GetOrCreateStorage(storageCell.Address);

            Db.Metrics.StorageTreeWrites++;
            
            _cache.Set(storageCell, value);
            tree.Set(storageCell.Index, value);
        }

        private void PushToRegistryOnly(in StorageCell cell, byte[] value)
        {
            StackList<int> stack = SetupRegistry(cell);
            IncrementChangePosition();
            stack.Push(_currentPosition);
            _originalValues[cell] = value;
            _changes[_currentPosition] = new Change(ChangeType.JustCache, cell, value);
        }

        private static void ReportChanges(IStorageTracer tracer, Dictionary<StorageCell, ChangeTrace> trace)
        {
            foreach ((StorageCell address, ChangeTrace change) in trace)
            {
                byte[] before = change.Before;
                byte[] after = change.After;

                if (!Bytes.AreEqual(before, after))
                {
                    tracer.ReportStorageChange(address, before, after);
                }
            }
        }

        private Hash256 RecalculateRootHash(Address address)
        {
            StorageTree storageTree = GetOrCreateStorage(address);
            storageTree.UpdateRootHash();
            return storageTree.RootHash;
        }

        /// <summary>
        /// Clear all storage at specified address
        /// </summary>
        /// <param name="address">Contract address</param>
        public override void ClearStorage(Address address)
        {
            base.ClearStorage(address);

            // here it is important to make sure that we will not reuse the same tree when the contract is revived
            // by means of CREATE 2 - notice that the cached trie may carry information about items that were not
            // touched in this block, hence were not zeroed above
            // TODO: how does it work with pruning?
            _storages.Set(address, new StorageTree(_trieStore.GetTrieStore(address.ToAccountPath), Keccak.EmptyTreeHash, _logManager));
        }

        public StorageTree Prefetch(Address address, Account account)
        {
            return _storages.Get(address, account);
        }

        internal void Prefetch(StorageTree tree, in StorageCell storageCell)
        {
            var value = tree.Get(storageCell.Index) ?? Array.Empty<byte>();
            _cache.Set(storageCell, value);
        }

        private class StorageTreeFactory : IStorageTreeFactory
        {
            public StorageTree Create(Address address, IScopedTrieStore trieStore, Hash256 storageRoot, Hash256 stateRoot, ILogManager? logManager)
                => new(trieStore, storageRoot, logManager);
        }

        private class StorageCache : IEnumerable<KeyValuePair<Address, StorageTree>>
        {
            private readonly PersistentStorageProvider _storage;
            private readonly IStorageTreeFactory _storageTreeFactory;
            private readonly Dictionary<Address, StorageTree>[] _caches;
            private bool _isReadOnly;

            public StorageCache(PersistentStorageProvider storage, IStorageTreeFactory storageTreeFactory)
            {
                // 16 caches to avoid contention in parallel processing
                Dictionary<Address, StorageTree>[] caches = new Dictionary<Address, StorageTree>[16];
                for (int i = 0; i < caches.Length; i++)
                {
                    caches[i] = new Dictionary<Address, StorageTree>(64);
                }

                _caches = caches;
                _storage = storage;
                _storageTreeFactory = storageTreeFactory;
            }

            public void Set(Address address, StorageTree tree)
            {
                if (_isReadOnly) return;

                int index = address.Bytes[0] & 0xF;
                Dictionary<Address, StorageTree> cache = _caches[index];
                lock (cache)
                {
                    cache[address] = tree;
                }
            }

            public StorageTree Get(Address address, Account? account = null)
            {
                int index = address.Bytes[0] & 0xF;
                Dictionary<Address, StorageTree> cache = _caches[index];
                lock (cache)
                {

                    ref StorageTree? value = ref CollectionsMarshal.GetValueRefOrAddDefault(cache, address, out bool exists);
                    if (!exists)
                    {
                        value = _storageTreeFactory.Create(address,
                                                           _storage._trieStore.GetTrieStore(address.ToAccountPath),
                                                           account?.StorageRoot ?? _storage._stateProvider.GetStorageRoot(address),
                                                           _storage.StateRoot,
                                                           _storage._logManager);
                        return value;
                    }

                    return value;
                }
            }

            public void Reset()
            {
                SetReadOnly();

                Dictionary<Address, StorageTree>[] caches = _caches;
                for (int i = 0; i < caches.Length; i++)
                {
                    caches[i] = new Dictionary<Address, StorageTree>(64);
                }
            }

            public void SetReadWrite()
            {
                Volatile.Write(ref _isReadOnly, false);
            }

            private void SetReadOnly()
            {
                Volatile.Write(ref _isReadOnly, true);
            }

            public IEnumerator<KeyValuePair<Address, StorageTree>> GetEnumerator()
            {
                Dictionary<Address, StorageTree>[] caches = _caches;
                for (int i = 0; i < caches.Length; i++)
                {
                    Dictionary<Address, StorageTree> cache = caches[i];
                    lock (cache)
                    {
                        foreach (KeyValuePair<Address, StorageTree> pair in cache)
                        {
                            yield return pair;
                        }
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
