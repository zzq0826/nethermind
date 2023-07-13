using System.Numerics;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Tools.Evm.Data;

public class Env
{
    public Address CurrentCoinbase { get; set; }
    public UInt64 CurrentGasLimit { get; set; }
    public UInt64 CurrentNumber { get; set; }
    public UInt64 CurrentTimestamp { get; set; }
    public List<Withdrawal> Withdrawals { get; set; }
    public BigInteger CurrentDifficulty { get; set; }
    public BigInteger CurrentRandom { get; set; }
    public BigInteger CurrentBaseFee { get; set; }
    public BigInteger ParentDifficulty { get; set; }
    public UInt64 ParentGasUsed { get; set; }
    public UInt64 ParentGasLimit { get; set; }
    public UInt64 ParentTimestamp { get; set; }
    public Dictionary<UInt64, Keccak> BlockHashes { get; set; }
    public Keccak ParentUncleHash { get; set; }
    public List<Ommer> Ommers { get; set; }
}

public class Ommer
{
    public UInt64 Delta { get; set; }
    public Address Address { get; set; }
}

public class Withdrawal
{
    public UInt64 Index { get; set; }
    public UInt64 ValidatorIndex { get; set; }
    public Address Recipient { get; set; }
    public BigInteger Amount { get; set; }
}