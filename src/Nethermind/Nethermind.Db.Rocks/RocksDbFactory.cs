// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Db.Rocks.Config;
using Nethermind.Logging;
using RocksDbSharp;

namespace Nethermind.Db.Rocks;

public class RocksDbFactory : IDbFactory, IDisposable
{
    private readonly IDbConfig _dbConfig;

    private readonly ILogManager _logManager;

    private readonly string _basePath;

    private readonly IntPtr _sharedCache;
    private readonly Env _env;

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

        _env = Env.CreateDefaultEnv();
        _env.SetBackgroundThreads(Math.Min(dbConfig.LowPriorityThreadCount, Environment.ProcessorCount));
        _env.SetHighPriorityBackgroundThreads(dbConfig.HighPriorityThreadCount);
        _sharedCache = RocksDbSharp.Native.Instance.rocksdb_cache_create_lru(new UIntPtr(dbConfig.SharedBlockCacheSize));
    }

    public IDb CreateDb(DbSettings dbSettings) =>
        new DbOnTheRocks(_basePath, dbSettings, _dbConfig, _logManager, sharedCache: _sharedCache, env: _env);

    public IColumnsDb<T> CreateColumnsDb<T>(DbSettings dbSettings) where T : struct, Enum =>
        new ColumnsDb<T>(_basePath, dbSettings, _dbConfig, _logManager, Array.Empty<T>(), sharedCache: _sharedCache, env: _env);

    public string GetFullDbPath(DbSettings dbSettings) => DbOnTheRocks.GetFullDbPath(dbSettings.DbPath, _basePath);

    public void Dispose()
    {
        Native.Instance.rocksdb_env_destroy(_env.Handle);
        Native.Instance.rocksdb_cache_destroy(_sharedCache);
    }
}
