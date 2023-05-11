// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Network.Enr;
using NUnit.Framework;

namespace Nethermind.Network.Dns.Test;

[TestFixture]
public class EnrRecordParserTest
{
    [Test]
    public void Test_ambiguous_decoding_point_to_same_Node_record()
    {
        // See: https://notes.ethereum.org/NG82G8rpQd-CEI9utZ63gg

        NodeRecordSigner singer = new(new Ecdsa(), TestItem.PrivateKeyA);
        EnrRecordParser parser = new(singer);
        string prefix =
            "enr:-IS4QHCYrYZbAKWCBRlAy5zzaDZXJBGkcnh4MHcBFZntXNFrdvJjX04jRzjzCBOonrkTfj499SZuOh8R33Ls8RRcy5wBgmlkgnY0gmlwhH8AAAGJc2VjcDI1NmsxoQPKY0yuDUmstAHYpMa2_oxVtw0RW_QAdpzBQA8yWM0xOIN1ZHCCdl";

        string baseEnr = prefix + "8";
        Dictionary<string, string> ambiguous = new()
        {
            { "padded", prefix + "8=" },
            { "trailingBits_1", prefix + "9" },
            { "trailingBits_2", prefix + "-" },
            { "trailingBits_3", prefix + "_" },
        };

        NodeRecord baseRecord = parser.ParseRecord(baseEnr);
        Console.WriteLine($"baseRecord.Enr ${baseRecord.EnrString}");
        foreach ((_, string enr) in ambiguous)
        {
            NodeRecord record = parser.ParseRecord(enr);
            Assert.That(baseRecord.EnrString, Is.EqualTo(record.EnrString));
        }
    }
}
