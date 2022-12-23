// Copyright 2022 Demerzel Solutions Limited
// Licensed under the LGPL-3.0. For full terms, see LICENSE-LGPL in the project root.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie.TrieBlend
{
    public class BlockDataLayer : IKeyValueStoreWithBatching
    {
        private readonly IKeyValueStoreWithBatching _keyValueStore;
        private readonly ConcurrentDictionary<byte[], byte[]> _rawData;
        private readonly ConcurrentDictionary<byte[], byte[]> _readCache;
        private readonly ConcurrentBag<byte[]> _dirty;
        private IBatch? _currentBatch;
        private bool _isLogPersisted;

        public long? BlockNumber;
        public bool IsPersisted => _dirty.IsEmpty;
        public bool IsLogPersisted => _dirty.IsEmpty;

        public int ElementCount => _rawData.Count;

        public BlockDataLayer(IKeyValueStoreWithBatching? keyValueStore, long? blockNumber = null)
        {
            _keyValueStore = keyValueStore ?? throw new ArgumentNullException(nameof(keyValueStore));
            _rawData = new ConcurrentDictionary<byte[], byte[]>(Bytes.EqualityComparer);
            _readCache = new ConcurrentDictionary<byte[], byte[]>(Bytes.EqualityComparer);
            _dirty = new ConcurrentBag<byte[]>();

            BlockNumber = blockNumber;
        }

        public byte[]? this[byte[] key]
        {
            get
            {
                return GetData(key);
            }
            set
            {
                _rawData[key] = value;
                _dirty.Add(key);
                _isLogPersisted = false;
            }
        }

        byte[]? IReadOnlyKeyValueStore.this[byte[] key] => GetData(key);

        public IBatch StartBatch()
        {
            _currentBatch ??= _keyValueStore.StartBatch();
            return _currentBatch;
        }

        public void DisposeBatch()
        {
            _currentBatch?.Dispose();
            _currentBatch = null;
        }

        public void Persist(long? blockNumber, bool persistLog = true)
        {
            long? useBlockNo = blockNumber ?? BlockNumber;
            BlockNumber = useBlockNo ?? throw new Exception("Can't persist layer without block number!");

            if (_currentBatch == null)
                throw new Exception("Batch not started");

            foreach (byte[] dirtyKey in _dirty)
            {
                _currentBatch[dirtyKey] = _rawData[dirtyKey];
            }
            _dirty.Clear();

            if (persistLog)
                PersistLog();
        }

        private void PersistLog()
        {
            if (_isLogPersisted)
                return;

            if (BlockNumber == null)
                throw new ArgumentNullException(nameof(BlockNumber));

            int contentLength = 0;
            int elementCount = 0;

            foreach (KeyValuePair<byte[], byte[]> item in _rawData)
            {
                contentLength += Rlp.LengthOfKeccakRlp + Rlp.LengthOf(item.Value);
                elementCount++;
            }

            contentLength += Rlp.LengthOf(BlockNumber?.ToBigEndianByteArray()) + Rlp.LengthOf(elementCount);
            int sequenceLength = Rlp.LengthOfSequence(contentLength);

            RlpStream rlpStream = new(contentLength);

            rlpStream.StartSequence(sequenceLength);
            rlpStream.Encode(BlockNumber.Value);
            rlpStream.Encode(elementCount);

            foreach (KeyValuePair<byte[], byte[]> item in _rawData)
            {
                rlpStream.Encode(item.Key);
                rlpStream.Encode(item.Value);
            }

            _currentBatch[BlockNumber?.ToBigEndianByteArray()] = rlpStream.Data;
            _isLogPersisted = true;
        }

        public void LoadLog(long blockNumber)
        {
            byte[] rawBytes = _keyValueStore[blockNumber.ToBigEndianByteArray()];
            if (rawBytes is null)
                return;

            BlockNumber = blockNumber;

            RlpStream rlpStream = new(rawBytes);
            rlpStream.ReadSequenceLength();
            long blockNo = rlpStream.DecodeLong();
            int accountsNo = rlpStream.DecodeInt();
            for (int i = 0; i < accountsNo; i++)
            {
                byte[] pathHash = rlpStream.DecodeByteArray();
                _rawData[pathHash] = rlpStream.DecodeByteArray();
            }
        }

        private byte[]? GetData(byte[] key)
        {
            if (!_rawData.TryGetValue(key, out byte[] dataValue))
            {
                return GetFromReadCache(key);
            }
            else
            {
                return dataValue;
            }
        }

        public byte[] GetFromReadCache(byte[] key)
        {
            if (!_readCache.TryGetValue(key, out byte[] dataValue))
            {
                byte[] dbData = _keyValueStore[key];
                if (dbData != null)
                {
                    _readCache[key] = dbData;
                    return dbData;
                }
            }
            return dataValue;
        }
    }
}
