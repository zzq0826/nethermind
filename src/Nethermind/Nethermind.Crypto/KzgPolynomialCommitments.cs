using System;
using System.IO;
using Nethermind.Core.Crypto;
using Nethermind.Crypto.Properties;
using Nethermind.Int256;

namespace Nethermind.Crypto
{
    public static class KzgPolynomialCommitments
    {
        public static readonly UInt256 BlsModulus = UInt256.Parse("73eda753299d7d483339d80809a1d80553bda402fffe5bfeffffffff00000001", System.Globalization.NumberStyles.HexNumber);
        public static readonly ulong FieldElementsPerBlob = 4096;

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
            return true;
            //fixed (byte* commitmentsPtr = commitments, blobsPtr = blobs, proofPtr = proof)
            //{
            //    return Ckzg.Ckzg.VerifyAggregateKzgProof(blobsPtr, commitmentsPtr, blobs.Length, proofPtr, CkzgSetup) == 0;
            //}
        }
    }
}
