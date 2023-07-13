using System.Numerics;
using Nethermind.Core;
using Nethermind.Core.Crypto;


namespace Nethermind.Tools.Evm.Data;
using AccessList = List<AccessTuple>;

public class LegacyTx
{
    public UInt64 Nonce { get; set; }
    public BigInteger GasPrice { get; set; }
    public UInt64 Gas { get; set; }
    public Address To { get; set; }
    public BigInteger Value { get; set; }
    public byte[] Data { get; set; }
    public BigInteger V { get; set; }
    public BigInteger R { get; set; }
    public BigInteger S { get; set; }
    public Keccak SecretKey { get; set; }
}

class AccessTuple
{
    public Address Address { get; set; }
    public Keccak[] StorageKeys { get; set; }
}

class AccessListTx
{
    public BigInteger ChainID { get; set; }
    public UInt64 Nonce { get; set; }
    public BigInteger GasPrice { get; set; }
    public UInt64 Gas { get; set; }
    public Address To { get; set; }
    public BigInteger Value { get; set; }
    public byte[] Data { get; set; }
    public AccessList AccessList { get; set; }
    public BigInteger V { get; set; }
    public BigInteger R { get; set; }
    public BigInteger S { get; set; }
    public Keccak SecretKey { get; set; }
}

class DynamicFeeTx
{
    public BigInteger ChainID { get; set; }
    public UInt64 Nonce { get; set; }
    public BigInteger GasTipCap { get; set; }
    public BigInteger GasFeeCap { get; set; }
    public UInt64 Gas { get; set; }
    public Address To { get; set; }
    public BigInteger Value { get; set; }
    public byte[] Data { get; set; }
    public AccessList AccessList { get; set; }
    public BigInteger V { get; set; }
    public BigInteger R { get; set; }
    public BigInteger S { get; set; }
    public Keccak SecretKey { get; set; }
}