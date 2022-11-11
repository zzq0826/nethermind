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
// 

using System;
using System.Collections;
using FluentAssertions;
using Nethermind.Core.Extensions;
using Nethermind.Crypto;
using Nethermind.Evm.Precompiles;
using Nethermind.Specs.Forks;
using NUnit.Framework;

namespace Nethermind.Evm.Test
{
    [TestFixture]
    public class PointEvaluationPrecompileTests
    {
        [TestCaseSource(typeof(TestCases))]
        public void Test(string testName, byte[] input, bool expectedResult, long expectedGasSpent, byte[] expectedOutput)
        {
            IPrecompile precompile = PointEvaluationPrecompile.Instance;
            (ReadOnlyMemory<byte> output, bool success) = precompile.Run(input, ShardingFork.Instance);
            long gasSpent = precompile.DataGasCost(input, ShardingFork.Instance) + precompile.BaseGasCost(ShardingFork.Instance);

            output.ToArray().Should().BeEquivalentTo(expectedOutput, testName);
            success.Should().Be(expectedResult, testName);
            gasSpent.Should().Be(expectedGasSpent, testName);
        }

        private static readonly byte[] _predefinedSuccessAnswer = Bytes.FromHexString("001000000000000001000000fffffffffe5bfeff02a4bd5305d8a10908d83933487d9d2953a7ed73");
        private static readonly byte[] _predefinedFailureAnswer = new byte[0];
        private static readonly long _fixedGasCost = 50000;

        public class TestCases : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                yield return new object[] {
                    "Incorrect data size",
                    Bytes.FromHexString("00"),
                    false,
                    _fixedGasCost,
                    _predefinedFailureAnswer
                };
                yield return new object[] {
                    "Incorrect data size",
                    Array.Empty<byte>(),
                    false,
                    _fixedGasCost,
                    _predefinedFailureAnswer
                };
                yield return new object[] {
                    "Hash is not in correctly versioned",
                    Bytes.FromHexString("02624652859a6e98ffc1608e2af0147ca4e86e1ce27672d8d3f3c9d4ffd6ef7e00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
                    false,
                    _fixedGasCost,
                    _predefinedFailureAnswer
                };
                yield return new object[] {
                    "X is out of range",
                    Bytes.FromHexString("01624652859a6e98ffc1608e2af0147ca4e86e1ce27672d8d3f3c9d4ffd6ef7e00000000000000000000000000000000000000000000000000000000000000ff0000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
                    false,
                    _fixedGasCost,
                    _predefinedFailureAnswer
                };
                yield return new object[] {
                    "Y is out of range",
                    Bytes.FromHexString("01624652859a6e98ffc1608e2af0147ca4e86e1ce27672d8d3f3c9d4ffd6ef7e000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000ffc00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
                    false,
                    _fixedGasCost,
                    _predefinedFailureAnswer
                };
                yield return new object[] {
                    "Commitment does not much hash",
                    Bytes.FromHexString("01624652859a6e98ffc1608e2af0147ca4e86e1ce27672d8d3f3c9d4ffd6ef7e00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
                    false,
                    _fixedGasCost,
                    _predefinedFailureAnswer
                };
                yield return new object[] {
                    "Proof does not match",
                    Bytes.FromHexString("01624652859a6e98ffc1608e2af0147ca4e86e1ce27672d8d3f3c9d4ffd6ef7e00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c20000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
                    false,
                    _fixedGasCost,
                    _predefinedFailureAnswer
                };
                yield return new object[] {
                    "Verification passes",
                    Bytes.FromHexString("01624652859a6e98ffc1608e2af0147ca4e86e1ce27672d8d3f3c9d4ffd6ef7e00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
                    true,
                    _fixedGasCost,
                    _predefinedSuccessAnswer
                };
            }
        }
    }
}
