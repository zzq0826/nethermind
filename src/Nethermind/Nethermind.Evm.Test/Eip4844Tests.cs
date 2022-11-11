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

using FluentAssertions;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Specs;
using Nethermind.Core.Test.Builders;
using NUnit.Framework;
using Nethermind.Core;
using System;
using Nethermind.Int256;

namespace Nethermind.Evm.Test
{
    public class Eip4844Tests : VirtualMachineTestsBase
    {
        protected override long BlockNumber => MainnetSpecProvider.GrayGlacierBlockNumber;
        protected override ulong Timestamp => MainnetSpecProvider.ShanghaiBlockTimestamp;

        [TestCase(0, 0, 0, Description = "Should return 0 when no hashes")]
        [TestCase(1, 1, 0, Description = "Should return 0 when out of range")]
        [TestCase(2, 1, 0, Description = "Should return 0 when way out of range")]
        [TestCase(0, 1, 10, Description = "Should return hash, when exists")]
        [TestCase(1, 3, 11, Description = "Should return hash, when exists")]
        public void Test_datahash_index_in_range(int index, int datahashesCount, ulong output)
        {
            byte[][] hashes = new byte[datahashesCount][];
            for (int i = 0; i < datahashesCount; i++)
            {
                hashes[i] = new byte[32];
                hashes[31][i] = (byte)(10 + i);
            }

            byte[] code = Prepare.EvmCode
                .PushData(index)
                .DATAHASH()
                .Done;
            TestAllTracerWithOutput result = Execute(BlockNumber, 50000, code, blobVersionedHashes: hashes, timestamp: Timestamp);

            result.StatusCode.Should().Be(1);
            new UInt256(result.ReturnValue).Should().Be(new UInt256(output));
            AssertGas(result, GasCostOf.Transaction + 3);
        }

        protected override TestAllTracerWithOutput CreateTracer()
        {
            TestAllTracerWithOutput tracer = base.CreateTracer();
            tracer.IsTracingAccess = false;
            return tracer;
        }

    }
}
