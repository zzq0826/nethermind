using System.Numerics;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Facade.Proxy.Models;

namespace Nethermind.Tools.Evm.Data;

public class ExecutionResult
{
    public Keccak StateRoot { get; set; }
    public Keccak TxRoot { get; set; }
    public Keccak ReceiptsRoot { get; set; }
    public Keccak LogsHash { get; set; }
    public Bloom LogsBloom { get; set; }
    public List<ReceiptModel> Receipts { get; set; }
    public List<RejectedTx> Rejected { get; set; }
    public BigInteger CurrentDifficulty { get; set; }
    public UInt64 GasUsed { get; set; }
    public BigInteger BaseFee { get; set; }
}

public class RejectedTx
{
    public int Index { get; set; }
    public string Err { get; set; }
}