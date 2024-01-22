// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nethermind.Core.Threading;

namespace Nethermind.Core.Collections;

public class ShardedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    private ReaderWriterLockSlim _globalLock;
    private McsLock[] _locks;
    private Dictionary<TKey, TValue>[] _dictionaries;
    private readonly int _shardCount;

    public ShardedDictionary(int shardCount)
    {
        _shardCount = shardCount;
        _globalLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        _locks = new McsLock[shardCount];
        _dictionaries = new Dictionary<TKey, TValue>[shardCount];
        for (int i = 0; i < shardCount; i++)
        {
            _locks[i] = new McsLock();
            _dictionaries[i] = new Dictionary<TKey, TValue>();
        }
    }

    public bool ContainsKey(in TKey key)
    {
        int shardIdx = CalculateShardIdx(key);
        _globalLock.EnterReadLock();
        try
        {
            using (_locks[shardIdx].Acquire())
            {
                return _dictionaries[shardIdx].ContainsKey(key);
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }
    }

    private int CalculateShardIdx(in TKey key)
    {
        int modded = key.GetHashCode() % _shardCount;
        if (modded < 0) modded += _shardCount;
        return modded;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        for (int i = 0; i < _shardCount; i++)
        {
            KeyValuePair<TKey, TValue>[] asArr;
            using (_locks[i].Acquire())
            {
                asArr = _dictionaries[i].ToArray();
            }

            foreach (KeyValuePair<TKey, TValue> keyValuePair in asArr)
            {
                yield return keyValuePair;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool TryGetValue(in TKey key, out TValue? value)
    {
        int shardIdx = CalculateShardIdx(key);
        _globalLock.EnterReadLock();
        try
        {
            using (_locks[shardIdx].Acquire())
            {
                return _dictionaries[shardIdx].TryGetValue(key, out value);
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }
    }

    public bool TryAdd(in TKey key, TValue value)
    {
        int shardIdx = CalculateShardIdx(key);
        _globalLock.EnterReadLock();
        try
        {
            using (_locks[shardIdx].Acquire())
            {
                return _dictionaries[shardIdx].TryAdd(key, value);
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }
    }

    public bool Remove(in TKey key, out TValue? value)
    {
        int shardIdx = CalculateShardIdx(key);
        _globalLock.EnterReadLock();
        try
        {
            using (_locks[shardIdx].Acquire())
            {
                return _dictionaries[shardIdx].Remove(key, out value);
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }
    }

    public void Clear()
    {
        _globalLock.EnterReadLock();
        try
        {
            for (int shardIdx = 0; shardIdx < _shardCount; shardIdx++)
            {
                using (_locks[shardIdx].Acquire())
                {
                    _dictionaries[shardIdx].Clear();
                }
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }
    }

    public GlobalLock AcquireLock()
    {
        _globalLock.EnterWriteLock();
        return new GlobalLock(_globalLock);
    }

    public struct GlobalLock
    {
        private readonly ReaderWriterLockSlim _theLock;

        public GlobalLock(ReaderWriterLockSlim globalLock)
        {
            _theLock = globalLock;
        }

        public void Dispose()
        {
            _theLock.ExitWriteLock();
        }
    }
}
