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

using NUnit.Framework;

namespace Nethermind.Trie.Test
{ 
    [TestFixture]
    public static class Lol
    {
       
        [Test]
        public static void InitOnce()
        {
            
            var a = new Nibble[]{ new (9) }.ToPackedByteArray();
            var b = new Nibble[]{ new (9),new (9) }.ToPackedByteArray();
            var c = new Nibble[]{ new (9),new (9),new (9) }.ToPackedByteArray();
        }
        
        public static byte[] ToPackedByteArray(byte[] nibbles)
        {
            int oddity = nibbles.Length % 2;
            byte[] bytes = new byte[nibbles.Length / 2 + 1];
            for (int i = 0; i < bytes.Length - 1; i++)
            {
                bytes[i + 1] = ToByte(nibbles[2 * i + oddity], nibbles[2 * i + 1 + oddity]);
            }

            if (oddity == 1)
            {
                bytes[0] = ToByte(1, nibbles[0]);
            }

            return bytes;
        }
        public static byte ToByte(byte highNibble, byte lowNibble)
        {
            return (byte)(((byte)highNibble << 4) | (byte)lowNibble);
        }
        
    }
}
