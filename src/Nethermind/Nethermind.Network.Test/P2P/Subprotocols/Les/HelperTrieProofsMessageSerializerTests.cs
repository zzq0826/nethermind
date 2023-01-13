// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Test.Builders;
using Nethermind.Network.P2P.Subprotocols.Les.Messages;
using Nethermind.Network.Test.P2P.Subprotocols.Eth.V62;
using Nethermind.Serialization.Rlp;
using NUnit.Framework;

namespace Nethermind.Network.Test.P2P.Subprotocols.Les
{
    [TestFixture]
    public class HelperTrieProofsMessageSerializerTests
    {
        [Test]
        public void RoundTrip()
        {
            byte[][] proofs = new byte[][]
            {
                TestItem.KeccakA.CreateByteArray,
                TestItem.KeccakB.CreateByteArray,
                TestItem.KeccakC.CreateByteArray,
                TestItem.KeccakD.CreateByteArray,
                TestItem.KeccakE.CreateByteArray,
                TestItem.KeccakF.CreateByteArray,
            };
            byte[][] auxData = new byte[][]
            {
                TestItem.KeccakG.CreateByteArray,
                TestItem.KeccakH.CreateByteArray,
                Rlp.Encode(Build.A.BlockHeader.TestObject).Bytes,
            };
            var message = new HelperTrieProofsMessage(proofs, auxData, 324, 734);

            HelperTrieProofsMessageSerializer serializer = new();

            SerializerTester.TestZero(serializer, message);
        }
    }
}
