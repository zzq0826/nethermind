// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Int256;
using Nethermind.Trie;
using Nethermind.Verkle;

namespace Nethermind.State;

// TODO: this can be definitely optimized by caching the keys from StateProvider - because for every access we
//       already calculate keys in StateProvider - or we maintain pre images?
public class VerkleWitness : IVerkleWitness
{
    [Flags]
    private enum AccountHeaderAccess
    {
        Version = 1,
        Balance = 2,
        Nonce = 4,
        CodeHash = 8,
        CodeSize = 16
    }

    private readonly JournalSet<byte[]> _accessedSubtrees;
    private readonly JournalSet<byte[]> _accessedLeaves;
    private readonly JournalSet<byte[]> _modifiedSubtrees;
    private readonly JournalSet<byte[]> _modifiedLeaves;

    // TODO: add these in GasPrices List
    private const long WitnessChunkRead = 200; // verkle-trie
    private const long WitnessChunkWrite = 500; // verkle-trie
    private const long WitnessChunkFill = 6200; // verkle-trie
    private const long WitnessBranchRead = 1900; // verkle-trie
    private const long WitnessBranchWrite = 3000; // verkle-trie

    private readonly Dictionary<int, int[]> _snapshots = new Dictionary<int, int[]>();
    private int NextSnapshot = 0;

    public VerkleWitness()
    {
        _accessedSubtrees = new JournalSet<byte[]>();
        _accessedLeaves = new JournalSet<byte[]>();
        _modifiedLeaves = new JournalSet<byte[]>();
        _modifiedSubtrees = new JournalSet<byte[]>();
    }
    /// <summary>
    /// When a non-precompile address is the target of a CALL, CALLCODE,
    /// DELEGATECALL, SELFDESTRUCT, EXTCODESIZE, or EXTCODECOPY opcode,
    /// or is the target address of a contract creation whose initcode
    /// starts execution.
    /// </summary>
    /// <param name="caller"></param>
    /// <returns></returns>
    public long AccessForCodeOpCodes(Address caller) => AccessAccount(caller, AccountHeaderAccess.Version | AccountHeaderAccess.CodeSize);

    /// <summary>
    /// Use this in two scenarios:
    /// 1. If a call is value-bearing (ie. it transfers nonzero wei), whether
    /// or not the callee is a precompile
    /// 2. If the SELFDESTRUCT/SENDALL opcode is called by some caller_address
    /// targeting some target_address (regardless of whether it’s value-bearing
    /// or not)
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="callee"></param>
    /// <returns></returns>
    public long AccessValueTransfer(Address caller, Address callee) => AccessAccount(caller, AccountHeaderAccess.Balance, true) + AccessAccount(callee, AccountHeaderAccess.Balance, true);

    /// <summary>
    /// When a contract creation is initialized.
    /// </summary>
    /// <param name="contractAddress"></param>
    /// <param name="isValueTransfer"></param>
    /// <returns></returns>
    public long AccessForContractCreationInit(Address contractAddress, bool isValueTransfer)
    {
        return isValueTransfer
            ? AccessAccount(contractAddress, AccountHeaderAccess.Version | AccountHeaderAccess.Nonce | AccountHeaderAccess.Balance, true)
            : AccessAccount(contractAddress, AccountHeaderAccess.Version | AccountHeaderAccess.Nonce, true);
    }

    /// <summary>
    /// When a contract is created.
    /// </summary>
    /// <param name="contractAddress"></param>
    /// <returns></returns>
    public long AccessContractCreated(Address contractAddress) => AccessCompleteAccount(contractAddress, true);

    /// <summary>
    /// If the BALANCE opcode is called targeting some address.
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public long AccessBalance(Address address) => AccessAccount(address, AccountHeaderAccess.Balance);

    /// <summary>
    /// If the EXTCODEHASH opcode is called targeting some address.
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public long AccessCodeHash(Address address) => AccessAccount(address, AccountHeaderAccess.CodeHash);

    /// <summary>
    /// When SLOAD and SSTORE opcodes are called with a given address
    /// and key.
    /// </summary>
    /// <param name="address"></param>
    /// <param name="key"></param>
    /// <param name="isWrite"></param>
    /// <returns></returns>
    public long AccessStorage(Address address, UInt256 key, bool isWrite) => AccessKey(AccountHeader.GetTreeKeyForStorageSlot(address.Bytes, key), isWrite);

    /// <summary>
    /// When the code chunk chunk_id is accessed is accessed
    /// </summary>
    /// <param name="address"></param>
    /// <param name="chunkId"></param>
    /// <param name="isWrite"></param>
    /// <returns></returns>
    public long AccessCodeChunk(Address address, byte chunkId, bool isWrite) => AccessKey(AccountHeader.GetTreeKeyForCodeChunk(address.Bytes, chunkId), isWrite);

    /// <summary>
    /// When you are starting to execute a transaction.
    /// </summary>
    /// <param name="originAddress"></param>
    /// <param name="destinationAddress"></param>
    /// <param name="isValueTransfer"></param>
    /// <returns></returns>
    public long AccessForTransaction(Address originAddress, Address? destinationAddress, bool isValueTransfer)
    {

        long gasCost = AccessCompleteAccount(originAddress) + (destinationAddress == null ? 0: AccessCompleteAccount(destinationAddress));

        // when you are executing a transaction, you are writing to the nonce of the origin address
        gasCost += AccessAccount(originAddress, AccountHeaderAccess.Nonce, true);
        if (isValueTransfer)
        {
            // when you are executing a transaction with value transfer,
            // you are writing to the balance of the origin and destination address
            gasCost += AccessValueTransfer(originAddress, destinationAddress);
        }

        return gasCost;
    }

    /// <summary>
    /// When you have to access the complete account
    /// </summary>
    /// <param name="address"></param>
    /// <param name="isWrite"></param>
    /// <returns></returns>
    public long AccessCompleteAccount(Address address, bool isWrite = false) => AccessAccount(address,
        AccountHeaderAccess.Version | AccountHeaderAccess.Balance | AccountHeaderAccess.Nonce | AccountHeaderAccess.CodeHash | AccountHeaderAccess.CodeSize,
        isWrite);

    /// <summary>
    /// When you have to access the certain keys for the account
    /// you can specify the keys you want to access using the AccountHeaderAccess.
    /// </summary>
    /// <param name="address"></param>
    /// <param name="accessOptions"></param>
    /// <param name="isWrite"></param>
    /// <returns></returns>
    private long AccessAccount(Address address, AccountHeaderAccess accessOptions, bool isWrite = false)
    {
        long gasUsed = 0;
        if ((accessOptions & AccountHeaderAccess.Version) == AccountHeaderAccess.Version) gasUsed += AccessKey(AccountHeader.GetTreeKey(address.Bytes, UInt256.Zero, AccountHeader.Version), isWrite);
        if ((accessOptions & AccountHeaderAccess.Balance) == AccountHeaderAccess.Balance) gasUsed += AccessKey(AccountHeader.GetTreeKey(address.Bytes, UInt256.Zero, AccountHeader.Balance), isWrite);
        if ((accessOptions & AccountHeaderAccess.Nonce) == AccountHeaderAccess.Nonce) gasUsed += AccessKey(AccountHeader.GetTreeKey(address.Bytes, UInt256.Zero, AccountHeader.Nonce), isWrite);
        if ((accessOptions & AccountHeaderAccess.CodeHash) == AccountHeaderAccess.CodeHash) gasUsed += AccessKey(AccountHeader.GetTreeKey(address.Bytes, UInt256.Zero, AccountHeader.CodeHash), isWrite);
        if ((accessOptions & AccountHeaderAccess.CodeSize) == AccountHeaderAccess.CodeSize) gasUsed += AccessKey(AccountHeader.GetTreeKey(address.Bytes, UInt256.Zero, AccountHeader.CodeSize), isWrite);
        return gasUsed;
    }

    private long AccessKey(byte[] key, bool isWrite = false, bool leafExist = false)
    {
        Debug.Assert(key.Length == 32);
        bool newSubTreeAccess = false;
        bool newLeafAccess = false;

        bool newSubTreeUpdate = false;
        bool newLeafUpdate = false;

        bool newLeafFill = false;


        if (_accessedLeaves.Add((key)))
        {
            newLeafAccess = true;
        }

        if (_accessedSubtrees.Add(key[..31]))
        {
            newSubTreeAccess = true;
        }

        long accessCost =
            (newLeafAccess ? WitnessChunkRead : 0) +
            (newSubTreeAccess ? WitnessBranchRead : 0);
        if (!isWrite)
            return accessCost;

        if (_modifiedLeaves.Add((key)))
        {
            newLeafFill = !leafExist;
            newLeafUpdate = true;
        }

        if (_modifiedSubtrees.Add(key[..31]))
        {
            newSubTreeUpdate = true;
        }
        long writeCost =
            (newLeafUpdate ? WitnessChunkWrite : 0) +
            (newLeafFill ? WitnessChunkFill : 0) +
            (newSubTreeUpdate ? WitnessBranchWrite : 0);

        return writeCost + accessCost;
    }

    public byte[][] GetAccessedKeys()
    {
        return _accessedLeaves.ToArray();
    }

    public int TakeSnapshot()
    {
        int[] snapshot = new int[2];
        snapshot[0] = _accessedSubtrees.TakeSnapshot();
        snapshot[1] = _accessedLeaves.TakeSnapshot();
        _snapshots.Add(NextSnapshot, snapshot);
        return NextSnapshot++;
    }

    public void Restore(int snapshot)
    {
        int[] witnessSnapshot = _snapshots[snapshot];
        _accessedSubtrees.Restore(witnessSnapshot[0]);
        _accessedLeaves.Restore(witnessSnapshot[1]);
    }
}
