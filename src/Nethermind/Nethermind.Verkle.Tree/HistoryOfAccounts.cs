// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using Nethermind.Core;
using Nethermind.Db;

namespace Nethermind.Verkle.Tree;

public class HistoryOfAccounts
{
    private IDb _historyOfAccounts;


    public HistoryOfAccounts(IDb historyOfAccounts)
    {
        _historyOfAccounts = historyOfAccounts;
    }

    public void AppendHistoryBlockNumberForAccount(Address address, ulong blockNumber)
    {

    }

    private struct HistoryKey
    {
        public Address Account;
        public ulong MaxBlock;

        public HistoryKey(Address address, ulong maxBlock)
        {
            Account = address;
            MaxBlock = maxBlock;
        }

        public byte[] Encode()
        {
            byte[] data = new byte[28];
            Span<byte> dataSpan = data;
            Account.Bytes.CopyTo(dataSpan);
            BinaryPrimitives.WriteUInt64LittleEndian(dataSpan.Slice(20), MaxBlock);
            return data;
        }
    }
}
