// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.TxPool;

namespace Nethermind.Consensus.Validators
{
    public class TxValidator : ITxValidator
    {
        private readonly ulong _chainIdValue;
        private readonly ILogger _logger;

        public TxValidator(ulong chainId, ILogManager logManager)
        {
            _chainIdValue = chainId;
            _logger = logManager?.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));
        }

        /* Full and correct validation is only possible in the context of a specific block
           as we cannot generalize correctness of the transaction without knowing the EIPs implemented
           and the world state (account nonce in particular ).
           Even without protocol change the tx can become invalid if another tx
           from the same account with the same nonce got included on the chain.
           As such we can decide whether tx is well formed but we also have to validate nonce
           just before the execution of the block / tx. */
        public bool IsWellFormed(Transaction transaction, IReleaseSpec releaseSpec)
        {
            // validate type before calculating intrinsic gas to avoid exception
            bool isTxTypeValid = ValidateTxType(transaction, releaseSpec);
            _logger.Info($"TxVALIDATION: isTxTypeValid: {isTxTypeValid}");

            /* This is unnecessarily calculated twice - at validation and execution times. */
            bool isGasLimitOk = transaction.GasLimit >= IntrinsicGasCalculator.Calculate(transaction, releaseSpec);
            _logger.Info($"TxVALIDATION: isGasLimitOk: {isGasLimitOk}, transaction.GasLimit: {transaction.GasLimit}, IntrinsicGasCalculator.Calculate(transaction, releaseSpec): {IntrinsicGasCalculator.Calculate(transaction, releaseSpec)}");

            bool signatureOk = ValidateSignature(transaction.Signature, releaseSpec);
            _logger.Info($"TxVALIDATION: signatureOk: {signatureOk}");

            bool chainIdOk = ValidateChainId(transaction);
            _logger.Info($"TxVALIDATION: chainIdOk: {chainIdOk}");

            bool are1559ok = Validate1559GasFields(transaction, releaseSpec);
            _logger.Info($"TxVALIDATION: are1559ok: {are1559ok}");
            _logger.Info($"{transaction.MaxFeePerGas} max fee");
            _logger.Info($"{transaction.MaxPriorityFeePerGas} max priority fee");

            bool ok3860Rules = Validate3860Rules(transaction, releaseSpec);
            _logger.Info($"TxVALIDATION: ok3860Rules: {ok3860Rules}");

            bool ok4844Fields = Validate4844Fields(transaction, releaseSpec);
            _logger.Info($"TxVALIDATION: ok4844Fields: {ok4844Fields}");


            return  isTxTypeValid &&
                    isGasLimitOk&&
                   /* if it is a call or a transfer then we require the 'To' field to have a value
                      while for an init it will be empty */
                   signatureOk &&
                   chainIdOk &&
                   are1559ok &&
                   ok3860Rules &&
                   ok4844Fields;
        }

        private static bool Validate3860Rules(Transaction transaction, IReleaseSpec releaseSpec)
        {
            bool isValid = !transaction.IsAboveInitCode(releaseSpec);
            return isValid;
        }

        private static bool ValidateTxType(Transaction transaction, IReleaseSpec releaseSpec) =>
            transaction.Type switch
            {
                TxType.Legacy => true,
                TxType.AccessList => releaseSpec.UseTxAccessLists,
                TxType.EIP1559 => releaseSpec.IsEip1559Enabled,
                TxType.Blob => releaseSpec.IsEip4844Enabled,
                _ => false
            };

        private static bool Validate1559GasFields(Transaction transaction, IReleaseSpec releaseSpec)
        {
            if (!releaseSpec.IsEip1559Enabled || !transaction.Supports1559)
                return true;

            bool isValid = transaction.MaxFeePerGas >= transaction.MaxPriorityFeePerGas;
            return isValid;
        }

        private bool ValidateChainId(Transaction transaction) =>
            transaction.Type switch
            {
                TxType.Legacy => true,
                _ => transaction.ChainId == _chainIdValue
            };

        private bool ValidateSignature(Signature? signature, IReleaseSpec spec)
        {
            if (signature is null)
            {
                return false;
            }

            UInt256 sValue = new(signature.SAsSpan, isBigEndian: true);
            UInt256 rValue = new(signature.RAsSpan, isBigEndian: true);

            if (sValue.IsZero || sValue >= (spec.IsEip2Enabled ? Secp256K1Curve.HalfNPlusOne : Secp256K1Curve.N))
            {
                return false;
            }

            if (rValue.IsZero || rValue >= Secp256K1Curve.NMinusOne)
            {
                return false;
            }

            if (spec.IsEip155Enabled)
            {
                return (signature.ChainId ?? _chainIdValue) == _chainIdValue;
            }

            return !spec.ValidateChainId || signature.V is 27 or 28;
        }

        private static bool Validate4844Fields(Transaction transaction, IReleaseSpec spec)
        {
            // Execution-payload version verification
            if (transaction.Type != TxType.Blob)
            {
                return transaction.MaxFeePerDataGas is null &&
                       transaction.BlobVersionedHashes is null &&
                       transaction is not { NetworkWrapper: ShardBlobNetworkWrapper };
            }

            if (transaction.MaxFeePerDataGas is null ||
                transaction.BlobVersionedHashes is null ||
                IntrinsicGasCalculator.CalculateDataGas(transaction.BlobVersionedHashes!.Length, spec) > Eip4844Constants.MaxDataGasPerTransaction ||
                transaction.BlobVersionedHashes!.Length < Eip4844Constants.MinBlobsPerTransaction)
            {
                return false;
            }

            for (int i = 0; i < transaction.BlobVersionedHashes!.Length; i++)
            {
                if (transaction.BlobVersionedHashes[i] is null ||
                    transaction.BlobVersionedHashes![i].Length !=
                    KzgPolynomialCommitments.BytesPerBlobVersionedHash ||
                    transaction.BlobVersionedHashes![i][0] != KzgPolynomialCommitments.KzgBlobHashVersionV1)
                {
                    return false;
                }
            }

            // Mempool version verification if presents
            if (transaction.NetworkWrapper is ShardBlobNetworkWrapper wrapper)
            {
                int blobCount = wrapper.Blobs.Length / Ckzg.Ckzg.BytesPerBlob;
                if (transaction.BlobVersionedHashes.Length != blobCount ||
                    (wrapper.Commitments.Length / Ckzg.Ckzg.BytesPerCommitment) != blobCount ||
                    (wrapper.Proofs.Length / Ckzg.Ckzg.BytesPerProof) != blobCount)
                {
                    return false;
                }

                Span<byte> hash = stackalloc byte[32];
                Span<byte> commitements = wrapper.Commitments;
                for (int i = 0, n = 0;
                     i < transaction.BlobVersionedHashes.Length;
                     i++, n += Ckzg.Ckzg.BytesPerCommitment)
                {
                    if (!KzgPolynomialCommitments.TryComputeCommitmentHashV1(
                            commitements[n..(n + Ckzg.Ckzg.BytesPerCommitment)], hash) ||
                        !hash.SequenceEqual(transaction.BlobVersionedHashes[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return true;
        }
    }
}
