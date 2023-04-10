using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Crypto;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Consensus.Bor;

public class BorAuthorRecoveryStep : IBlockPreprocessorStep
{
    private const int ExtraSeal = 65;

    private readonly BorParameters _params;
    private readonly IEthereumEcdsa _ecdsa;
    private readonly ICache<KeccakKey, Address> _signatures;

    public BorAuthorRecoveryStep(IEthereumEcdsa ecdsa, BorParameters borParams, int cacheSize = 1024)
    {
        _ecdsa = ecdsa;
        _params = borParams;
        _signatures = new LruCache<KeccakKey, Address>(cacheSize, "BorSignatureCache");
    }

    public void RecoverData(Block block)
    {
        BlockHeader header = block.Header;

        ArgumentNullException.ThrowIfNull(header.Hash);
        ArgumentNullException.ThrowIfNull(header.ExtraData);

        if (header.Author is not null)
            return;

        if (_signatures.TryGet(header.Hash, out Address? addr))
        {
            // the throw bellow should never happen, but the TryGet signature is confusing
            block.Header.Author = addr ?? throw new InvalidOperationException("Author cannot be null");
            return;
        }

        // Retrieve the signature from the header extra-data
        if (header.ExtraData.Length < ExtraSeal)
            throw new BlockchainException($"Bor block without sealer extra data{Environment.NewLine}{header.ToString(BlockHeader.Format.Full)}");

        Span<byte> signature = header.ExtraData.AsSpan(header.ExtraData.Length - ExtraSeal, ExtraSeal);
        Keccak message = CalculateBorHeaderHash(header);

        header.Author = _ecdsa.RecoverAddress(signature, message) ?? throw new SealEngineException("Invalid signature");

        _signatures.Set(header.Hash, header.Author);
    }

    public Keccak CalculateBorHeaderHash(BlockHeader header)
    {
        Span<byte> extra = header.ExtraData.AsSpan(0, header.ExtraData.Length - ExtraSeal);
        List<Rlp> encoding = new()
        {
            Rlp.Encode(header.ParentHash),
            Rlp.Encode(header.UnclesHash),
            Rlp.Encode(header.Beneficiary),
            Rlp.Encode(header.StateRoot),
            Rlp.Encode(header.TxRoot),
            Rlp.Encode(header.ReceiptsRoot),
            Rlp.Encode(header.Bloom),
            Rlp.Encode(header.Difficulty),
            Rlp.Encode(header.Number),
            Rlp.Encode(header.GasLimit),
            Rlp.Encode(header.GasUsed),
            Rlp.Encode(header.Timestamp),
            Rlp.Encode(extra),
            Rlp.Encode(header.MixHash),
            Rlp.EncodeNonce(header.Nonce)
        };

        if (header.Number >= (_params.JaipurBlockNumber ?? long.MaxValue))
            encoding.Add(Rlp.Encode(header.BaseFeePerGas));

        return Keccak.Compute(Rlp.Encode(encoding.ToArray()).Bytes);
    }
}