// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethermind.Core;
using NUnit.Framework.Internal;
using Nethermind.Core.Test.Builders;
using NSubstitute;
using Nethermind.Core.Crypto;

namespace Nethermind.Era1.Test;
internal class EraBuilderTests
{
    [Test]
    public void Add_TotalDifficultyIsLowerThanBlock_ThrowsException()
    {
        using MemoryStream stream = new();
        EraBuilder sut = EraBuilder.Create(stream);

        Assert.That(async () => await sut.Add(
            Keccak.Zero,
            Array.Empty<byte>(),
            Array.Empty<byte>(),
            Array.Empty<byte>(),
            1,
            1,
            0), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public async Task Add_AddOneBlock_ReturnsTrue()
    {
        using MemoryStream stream = new();
        EraBuilder sut = EraBuilder.Create(stream);
        Block block1 = Build.A.Block.WithNumber(1)
            .WithTotalDifficulty(BlockHeaderBuilder.DefaultDifficulty).TestObject;

        bool result = await sut.Add(block1, Array.Empty<TxReceipt>());

        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public async Task Add_AddMoreThanMaximumOf8192_ReturnsFalse()
    {
        using MemoryStream stream = Substitute.For<MemoryStream>();
        stream.WriteAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>()).Returns(Task.CompletedTask);

        EraBuilder sut = EraBuilder.Create(stream);
        for (int i = 0; i < EraBuilder.MaxEra1Size; i++)
        {
            Block block = Build.A.Block.WithNumber(0)
            .WithTotalDifficulty(BlockHeaderBuilder.DefaultDifficulty).TestObject;
            await sut.Add(block, Array.Empty<TxReceipt>());
        }

        bool result = await sut.Add(Build.A.Block.WithNumber(0)
            .WithTotalDifficulty(BlockHeaderBuilder.DefaultDifficulty).TestObject, Array.Empty<TxReceipt>());

        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public async Task Add_FinalizedCalled_ThrowsException()
    {
        using MemoryStream stream = new();
        EraBuilder sut = EraBuilder.Create(stream);
        Block block = Build.A.Block.WithNumber(1)
            .WithTotalDifficulty(BlockHeaderBuilder.DefaultDifficulty).TestObject;

        await sut.Add(block, Array.Empty<TxReceipt>());
        await sut.Finalize();

        Assert.That(async () => await sut.Add(block, Array.Empty<TxReceipt>()), Throws.TypeOf<EraException>());
    }
    [Test]
    public void Finalize_NoBlocksAdded_ThrowsException()
    {
        using MemoryStream stream = new();

        EraBuilder sut = EraBuilder.Create(stream);

        Assert.That(async () => await sut.Finalize(), Throws.TypeOf<EraException>());
    }

    [Test]
    public async Task Finalize_AddOneBlock_WritesAccumulatorEntry()
    {
        using MemoryStream stream = new();
        EraBuilder sut = EraBuilder.Create(stream);
        await sut.Add(
            Keccak.Zero,
            Array.Empty<byte>(),
            Array.Empty<byte>(),
            Array.Empty<byte>(),
            0,
            0,
            0);
        byte[] buffer = new byte[40];

        await sut.Finalize();
        stream.Seek(-buffer.Length - 4 * 8, SeekOrigin.End);
        stream.Read(buffer, 0, buffer.Length);

        Assert.That(BitConverter.ToUInt16(buffer), Is.EqualTo(EntryTypes.Accumulator));
    }

    [Test]
    public void Dispose_Disposed_InnerStreamIsDisposed()
    {
        using MemoryStream stream = new();
        EraBuilder sut = EraBuilder.Create(stream);

        sut.Dispose();

        Assert.That(() => stream.ReadByte(), Throws.TypeOf<ObjectDisposedException>());
    }

    [TestCase("test", 0, "0x0000000000000000000000000000000000000000000000000000000000000000", "test-00000-00000000.era1")]
    [TestCase("goerli", 1, "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", "goerli-00001-ffffffff.era1")]
    [TestCase("sepolia", 2, "0x1122ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", "sepolia-00002-1122ffff.era1")]
    public void Filename_ValidParameters_ReturnsExpected(string network, int epoch, string hash, string expected)
    {
        Assert.That(EraBuilder.Filename(network, epoch, new Keccak(hash)), Is.EqualTo(expected));
    }

    [Test]
    public void Filename_NetworkIsNull_ReturnsException()
    {
        Assert.That(() => EraBuilder.Filename(null, 0, new Keccak("0x0000000000000000000000000000000000000000000000000000000000000000")), Throws.ArgumentException);
    }

    [Test]
    public void Filename_NetworkIsEmpty_ReturnsException()
    {
        Assert.That(() => EraBuilder.Filename("", 0, new Keccak("0x0000000000000000000000000000000000000000000000000000000000000000")), Throws.ArgumentException);
    }

    [Test]
    public void Filename_EpochIsNegative_ReturnsException()
    {
        Assert.That(() => EraBuilder.Filename("test", -1, new Keccak("0x0000000000000000000000000000000000000000000000000000000000000000")), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Filename_RootIsNull_ReturnsException()
    {
        Assert.That(() => EraBuilder.Filename("test", 0, null), Throws.ArgumentNullException);
    }

}
