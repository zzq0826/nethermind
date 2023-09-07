// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Db.Rocks.Config;
using Nethermind.Logging;
using RocksDbSharp;

namespace Nethermind.Db.Rocks;

public class RocksDbFactory : IRocksDbFactory
{
    private readonly IDbConfig _dbConfig;

    private readonly ILogManager _logManager;

    private readonly string _basePath;

    private IntPtr _sharedCache;
    private readonly IntPtr? _allocator;

    public RocksDbFactory(IDbConfig dbConfig, ILogManager logManager, string basePath)
    {
        _dbConfig = dbConfig;
        _logManager = logManager;
        _basePath = basePath;

        ILogger logger = _logManager.GetClassLogger<RocksDbFactory>();

        if (logger.IsDebug)
        {
            logger.Debug($"Shared memory size is {dbConfig.SharedBlockCacheSize}");
        }

        Native rocksNative = Native.Instance;
        IntPtr lruOpts = rocksNative.rocksdb_lru_cache_options_create();
        rocksNative.rocksdb_lru_cache_options_set_capacity(lruOpts, (UIntPtr)dbConfig.SharedBlockCacheSize);

        try
        {
            _allocator = rocksNative.rocksdb_jemalloc_nodump_allocator_create();
        }
        catch (RocksDbException e)
        {
            if (e.Message.Contains("Not compiled with JEMALLOC"))
            {
                if (logger.IsDebug) logger.Debug("RockDB not compiled with Jemalloc. Expect higher memory usage.");
            }
            else
            {
                throw;
            }
        }

        if (_allocator != null)
        {
            rocksNative.rocksdb_lru_cache_options_set_memory_allocator(lruOpts, _allocator.Value);
        }

        _sharedCache = rocksNative.rocksdb_cache_create_lru_opts(lruOpts);
    }

    public IDb CreateDb(RocksDbSettings rocksDbSettings) =>
        new DbOnTheRocks(_basePath, rocksDbSettings, _dbConfig, _logManager, sharedCache: _sharedCache, allocator: _allocator);

    public IColumnsDb<T> CreateColumnsDb<T>(RocksDbSettings rocksDbSettings) where T : struct, Enum =>
        new ColumnsDb<T>(_basePath, rocksDbSettings, _dbConfig, _logManager, Array.Empty<T>(), sharedCache: _sharedCache, allocator: _allocator);

    public string GetFullDbPath(RocksDbSettings rocksDbSettings) => DbOnTheRocks.GetFullDbPath(rocksDbSettings.DbPath, _basePath);
}
