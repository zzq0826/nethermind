using System.Numerics;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Tools.Evm.Data;
using Alloc = Dictionary<Address,Account>;
public class Account
{
    public byte[] Code { get; set; }
    public Dictionary<Keccak, Keccak> Storage { get; set; }
    public BigInteger Balance { get; set; }
    public UInt64 Nonce { get; set; }
    public byte[] SecretKey { get; set; }
}