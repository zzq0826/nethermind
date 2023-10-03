// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test;
using NUnit.Framework;

namespace Nethermind.Synchronization.Test.SnapSync;

public class SnapWriteBatchPacerTests
{
    [Test]
    public void TestBasicReadWrite()
    {
        TestMemDb baseDb = new();
        SnapWriteBatchPacer pacer = new(baseDb);

        for (int i = 0; i < 1000; i++)
        {
            pacer.Set(i.ToBigEndianByteArray(), i.ToBigEndianByteArray());
        }

        for (int i = 0; i < 1000; i++)
        {
            pacer.Get(i.ToBigEndianByteArray()).Should().BeEquivalentTo(i.ToBigEndianByteArray());
        }
    }

    [Test]
    public void TestBasicReadWrite_WithWriteBatchWrite()
    {
        TestMemDb baseDb = new();
        SnapWriteBatchPacer pacer = new(baseDb);

        IBatch writeBatch = pacer.StartBatch();
        for (int i = 0; i < 1000; i++)
        {
            writeBatch.Set(i.ToBigEndianByteArray(), i.ToBigEndianByteArray());
        }

        // Early writes will flush even before dispose
        for (int i = 0; i < 100; i++)
        {
            pacer.Get(i.ToBigEndianByteArray()).Should().BeEquivalentTo(i.ToBigEndianByteArray());
        }

        writeBatch.Dispose();
        pacer.Flush();

        for (int i = 0; i < 1000; i++)
        {
            pacer.Get(i.ToBigEndianByteArray()).Should().BeEquivalentTo(i.ToBigEndianByteArray());
        }
    }

    [Test]
    public void TestDoesNotWriteManySmallBatch()
    {
        TestMemDb baseDb = new();
        SnapWriteBatchPacer pacer = new(baseDb);

        for (int i = 0; i < 20; i++)
        {
            IBatch writeBatch = pacer.StartBatch();
            writeBatch.Set(i.ToBigEndianByteArray(), i.ToBigEndianByteArray());
            writeBatch.Dispose();
        }

        for (int i = 0; i < 20; i++)
        {
            pacer.Get(i.ToBigEndianByteArray()).Should().BeNull();
        }

        pacer.Flush();
        baseDb.TotalWriteBatch.Should().Be(1);

        for (int i = 0; i < 20; i++)
        {
            pacer.Get(i.ToBigEndianByteArray()).Should().BeEquivalentTo(i.ToBigEndianByteArray());
        }
    }

}
