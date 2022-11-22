using System;
using System.IO;
using System.Runtime.InteropServices;
using Nethermind.Core.Crypto;
using Nethermind.Crypto.Properties;
using Nethermind.Int256;

namespace Nethermind.Crypto
{
    public static class KzgPolynomialCommitments
    {
        public static readonly UInt256 BlsModulus = UInt256.Parse("73eda753299d7d483339d80809a1d80553bda402fffe5bfeffffffff00000001", System.Globalization.NumberStyles.HexNumber);
        public static readonly ulong FieldElementsPerBlob = Ckzg.Ckzg.BlobLength / Ckzg.Ckzg.BlobElementLength;

        private const byte KzgBlobHashVersionV1 = 1;
        private static IntPtr CkzgSetup = IntPtr.Zero;

        private static object InititalizeLock = new object();

        public static void Inititalize()
        {
            lock (InititalizeLock)
            {
                if (CkzgSetup != IntPtr.Zero)
                {
                    return;
                }
                string tmpFilename = Path.GetTempFileName();
                using FileStream tmpFileStream = new(tmpFilename, FileMode.OpenOrCreate, FileAccess.Write);
                using TextWriter tmpFileWriter = new StreamWriter(tmpFileStream);
                tmpFileWriter.Write(Resources.kzg_trusted_setup);
                tmpFileWriter.Close();
                tmpFileStream.Close();
                CkzgSetup = Ckzg.Ckzg.LoadTrustedSetup(tmpFilename);
                File.Delete(tmpFilename);
            }
        }

        public static Span<byte> CommitmentToHashV1(ReadOnlySpan<byte> data_kzg)
        {
            Keccak hash = Keccak.Compute(data_kzg);
            hash.Bytes[0] = KzgBlobHashVersionV1;
            return hash.Bytes;
        }

        public static bool VerifyHashV1Format(ReadOnlySpan<byte> hash)
        {
            return hash[0] == KzgBlobHashVersionV1;
        }

        public static unsafe bool VerifyProof(ReadOnlySpan<byte> commitment, ReadOnlySpan<byte> x, ReadOnlySpan<byte> y, ReadOnlySpan<byte> proof)
        {
            fixed (byte* commitmentPtr = commitment, xPtr = x, yPtr = y, proofPtr = proof)
            {
                return Ckzg.Ckzg.VerifyKzgProof(commitmentPtr, xPtr, yPtr, proofPtr, CkzgSetup) == 0;
            }
        }

        public static unsafe bool IsAggregatedProofValid(byte[] proof, byte[][] blobs, byte[][] commitments)
        {
            if(proof == null && blobs == null && commitments == null)
            {
                return true;
            }
            if (proof == null || blobs == null || commitments == null)
            {
                return false;
            }
            if (blobs.Length != commitments.Length)
            {
                return false;
            }

            byte[] flattenCommitments = new byte[commitments.Length * Ckzg.Ckzg.CommitmentLength];
            for (int i = 0; i < commitments.Length; i++)
            {
                commitments[i].CopyTo(flattenCommitments, i * Ckzg.Ckzg.CommitmentLength);
            }
            byte[] flattenBlobs = new byte[blobs.Length * Ckzg.Ckzg.BlobLength];
            for (int i = 0; i < blobs.Length; i++)
            {
                blobs[i].CopyTo(flattenBlobs, i * Ckzg.Ckzg.BlobLength);
            }
            fixed (byte* commitmentsPtr = flattenCommitments, blobsPtr = flattenBlobs, proofPtr = proof)
            {
                return Ckzg.Ckzg.VerifyAggregatedKzgProof(blobsPtr, commitmentsPtr, blobs.Length, proofPtr, CkzgSetup) == 0;
            }
        }
    }
}
