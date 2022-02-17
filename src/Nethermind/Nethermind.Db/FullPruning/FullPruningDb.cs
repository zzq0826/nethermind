//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Db.FullPruning
{
    /// <summary>
    /// Database facade that allows full pruning.
    /// </summary>
    /// <remarks>
    /// Allows to start pruning with <see cref="TryStartPruning"/> in a thread safe way.
    /// When pruning is started it duplicates all writes to current DB as well as the new one for full pruning, this includes write batches.
    /// When <see cref="IPruningContext"/> returned in <see cref="TryStartPruning"/> is <see cref="IDisposable.Dispose"/>d it will delete the pruning DB if the pruning was not successful.
    /// It uses <see cref="IRocksDbFactory"/> to create new pruning DB's. Check <see cref="FullPruningInnerDbFactory"/> to see how inner sub DB's are organised.
    /// </remarks>
    public class FullPruningDb : IDb, IFullPruningDb
    {
        private readonly RocksDbSettings _settings;
        private readonly IRocksDbFactory _dbFactory;
        private readonly Action _updateDuplicateWriteMetrics;

        // current main DB, will be written to and will be main source for reading
        private IDb _currentDb;
        
        // main DB regardless of pruning
        private IDb _mainDb;
        
        // current pruning context, secondary DB that the state will be written to, as well as state trie will be copied to
        // this will be null if no full pruning is in progress
        private PruningContext? _pruningContext;

        public FullPruningDb(RocksDbSettings settings, IRocksDbFactory dbFactory, Action? updateDuplicateWriteMetrics = null)
        {
            _settings = settings;
            _dbFactory = dbFactory;
            _updateDuplicateWriteMetrics = updateDuplicateWriteMetrics ?? (() => { });
            _mainDb = _currentDb = CreateDb(_settings);
        }

        private IDb CreateDb(RocksDbSettings settings) => _dbFactory.CreateDb(settings);

        public byte[]? this[byte[] key]
        {
            get => _currentDb[key];
            set => _currentDb[key] = value;
        }

        // we also need to duplicate writes that are in batches
        public IBatch StartBatch() => _currentDb.StartBatch();

        public void Dispose() => _currentDb.Dispose();

        public string Name => _settings.DbName;
        
        public IEnumerable<KeyValuePair<byte[], byte[]>> GetAll(bool ordered = false) => _currentDb.GetAll(ordered);

        public IEnumerable<byte[]> GetAllValues(bool ordered = false) => _currentDb.GetAllValues(ordered);

        // we need to remove from both DB's
        public void Remove(byte[] key) => _currentDb.Remove(key);

        public bool KeyExists(byte[] key) => _currentDb.KeyExists(key);

        // we need to flush both DB's
        public void Flush() => _currentDb.Flush();

        // we need to clear both DB's
        public void Clear() => _currentDb.Clear();

        /// <inheritdoc />
        public bool CanStartPruning => _pruningContext is null; // we can start pruning only if no pruning is in progress

        public bool TryStartPruning(out IPruningContext context) => TryStartPruning(true, out context);
        
        /// <inheritdoc />
        public virtual bool TryStartPruning(bool duplicateReads, out IPruningContext context)
        {
            RocksDbSettings ClonedDbSettings()
            {
                RocksDbSettings clonedDbSettings = _settings.Clone();
                clonedDbSettings.DeleteOnStart = true;
                return clonedDbSettings;
            }
            
            // create new pruning context with new sub DB and try setting it as current
            // returns true when new pruning is started
            // returns false only on multithreaded access, returns started pruning context then
            IDb cloningDb = CreateDb(ClonedDbSettings());
            IDb duplicatingDb = duplicateReads 
                ? new DuplicatingDbWithReads(_mainDb, cloningDb, _updateDuplicateWriteMetrics)
                : new DuplicatingDbWithoutReads(_mainDb, cloningDb, _updateDuplicateWriteMetrics);
            
            PruningContext newContext = new(this, cloningDb, _updateDuplicateWriteMetrics);
            PruningContext? pruningContext = Interlocked.CompareExchange(ref _pruningContext, newContext, null);
            context = pruningContext ?? newContext;
            if (pruningContext is null)
            {
                _currentDb = duplicatingDb;
                PruningStarted?.Invoke(this, new PruningEventArgs(context));
                return true;
            }

            return false;
        }
        
        /// <inheritdoc />
        public string GetPath(string basePath) => _settings.DbPath.GetApplicationResourcePath(basePath);
        
        /// <inheritdoc />
        public string InnerDbName => _currentDb.Name;

        public event EventHandler<PruningEventArgs>? PruningStarted;
        public event EventHandler<PruningEventArgs>? PruningFinished;

        private void FinishPruning(PruningContext pruningContext)
        {
            if (Interlocked.CompareExchange(ref _pruningContext, null, pruningContext) == pruningContext)
            {
                IDb cloningDb = pruningContext.CloningDb;
                
                if (pruningContext.Committed)
                {
                    Interlocked.Exchange(ref _currentDb, cloningDb);
                    IDb? oldDb = _mainDb;
                    _mainDb = cloningDb;
                    
                    // if was committed, then pruning is finished and we delete old main DB
                    ClearOldDb(oldDb);

                }
                else
                {
                    Interlocked.Exchange(ref _currentDb, _mainDb);
                    
                    // if was not committed, then pruning failed and we delete the cloned DB
                    cloningDb.Clear();
                }
                PruningFinished?.Invoke(this, new PruningEventArgs(pruningContext));
            }
        }

        protected virtual void ClearOldDb(IDb oldDb)
        {
            oldDb.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void Duplicate(IKeyValueStore db, byte[] key, byte[]? value, Action updateDuplicateWriteMetrics)
        {
            db[key] = value;
            updateDuplicateWriteMetrics();
        }
        
        private class PruningContext : IPruningContext
        {
            public bool Committed { get; private set; } = false;
            private bool _disposed = false;
            public IDb CloningDb { get; }
            private readonly FullPruningDb _db;
            private readonly Action _updateDuplicateWriteMetrics;

            public PruningContext(FullPruningDb db, IDb cloningDb, Action updateDuplicateWriteMetrics)
            {
                CloningDb = cloningDb;
                _db = db;
                _updateDuplicateWriteMetrics = updateDuplicateWriteMetrics;
            }

            /// <inheritdoc />
            public byte[]? this[byte[] key]
            {
                get => CloningDb[key];
                set => Duplicate(CloningDb, key, value, _updateDuplicateWriteMetrics);
            }

            /// <inheritdoc />
            public void Commit()
            {
                Committed = true; // we mark the context as committed.
            }

            /// <inheritdoc />
            public void MarkStart()
            {
                Metrics.StateDbPruning = 1;
            }

            public CancellationTokenSource CancellationTokenSource { get; } = new();

            /// <inheritdoc />
            public void Dispose()
            {
                if (!_disposed)
                {
                    _db.FinishPruning(this);
                    CancellationTokenSource.Dispose();
                    Metrics.StateDbPruning = 0;
                    _disposed = true;
                }
            }
        }

        private class DuplicatingDbWithReads : IDb
        {
            private readonly IDb _mainDb;
            private readonly IDb _duplicatingDb;
            private readonly Action _updateDuplicateWriteMetrics;

            public DuplicatingDbWithReads(IDb mainDb, IDb duplicatingDb, Action updateDuplicateWriteMetrics)
            {
                _mainDb = mainDb;
                _duplicatingDb = duplicatingDb;
                _updateDuplicateWriteMetrics = updateDuplicateWriteMetrics;
            }

            public byte[]? this[byte[] key]
            {
                get
                {
                    byte[]? value = _mainDb[key];
                    Duplicate(_duplicatingDb, key, value, _updateDuplicateWriteMetrics);
                    return value; 
                }
                set
                {
                    _mainDb[key] = value;
                    Duplicate(_duplicatingDb, key, value, _updateDuplicateWriteMetrics);
                }
            }

            public IBatch StartBatch() => new DuplicatingBatch(_mainDb.StartBatch(), _duplicatingDb.StartBatch(), _updateDuplicateWriteMetrics);

            public void Dispose()
            {
                _mainDb.Dispose();
                _duplicatingDb.Dispose();
            }

            public string Name => _mainDb.Name;

            public IEnumerable<KeyValuePair<byte[], byte[]>> GetAll(bool ordered = false) => _mainDb.GetAll(ordered);

            public IEnumerable<byte[]> GetAllValues(bool ordered = false) => _mainDb.GetAllValues(ordered);

            public void Remove(byte[] key)
            {
                _mainDb.Remove(key);
                _duplicatingDb.Remove(key);
            }

            public bool KeyExists(byte[] key) => _mainDb.KeyExists(key);

            public void Flush()
            {
                _mainDb.Flush();
                _duplicatingDb.Flush();
            }

            public void Clear()
            {
                _mainDb.Clear();
                _duplicatingDb.Clear();
            }
        }
        
        private class DuplicatingDbWithoutReads : IDb
        {
            private readonly IDb _mainDb;
            private readonly IDb _duplicatingDb;
            private readonly Action _updateDuplicateWriteMetrics;

            public DuplicatingDbWithoutReads(IDb mainDb, IDb duplicatingDb, Action updateDuplicateWriteMetrics)
            {
                _mainDb = mainDb;
                _duplicatingDb = duplicatingDb;
                _updateDuplicateWriteMetrics = updateDuplicateWriteMetrics;
            }

            public byte[]? this[byte[] key]
            {
                get => _mainDb[key];
                set
                {
                    _mainDb[key] = value;
                    Duplicate(_duplicatingDb, key, value, _updateDuplicateWriteMetrics);
                }
            }

            public IBatch StartBatch() => new DuplicatingBatch(_mainDb.StartBatch(), _duplicatingDb.StartBatch(), _updateDuplicateWriteMetrics);

            public void Dispose()
            {
                _mainDb.Dispose();
                _duplicatingDb.Dispose();
            }

            public string Name => _mainDb.Name;

            public IEnumerable<KeyValuePair<byte[], byte[]>> GetAll(bool ordered = false) => _mainDb.GetAll(ordered);

            public IEnumerable<byte[]> GetAllValues(bool ordered = false) => _mainDb.GetAllValues(ordered);

            public void Remove(byte[] key)
            {
                _mainDb.Remove(key);
                _duplicatingDb.Remove(key);
            }

            public bool KeyExists(byte[] key) => _mainDb.KeyExists(key);

            public void Flush()
            {
                _mainDb.Flush();
                _duplicatingDb.Flush();
            }

            public void Clear()
            {
                _mainDb.Clear();
                _duplicatingDb.Clear();
            }
        }

        /// <summary>
        /// Batch that duplicates writes to the current DB and the cloned DB batches.
        /// </summary>
        private class DuplicatingBatch : IBatch
        {
            private readonly IBatch _batch;
            private readonly IBatch _duplicatingBatch;
            private readonly Action _updateDuplicateWriteMetrics;

            public DuplicatingBatch(
                IBatch batch, 
                IBatch duplicatingBatch, 
                Action updateDuplicateWriteMetrics)
            {
                _batch = batch;
                _duplicatingBatch = duplicatingBatch;
                _updateDuplicateWriteMetrics = updateDuplicateWriteMetrics;
            }

            public void Dispose()
            {
                _batch.Dispose();
                _duplicatingBatch.Dispose();
            }

            public byte[]? this[byte[] key]
            {
                get => _batch[key];
                set
                {
                    _batch[key] = value;
                    Duplicate(_duplicatingBatch, key, value, _updateDuplicateWriteMetrics);
                }
            }
        }
    }
}
