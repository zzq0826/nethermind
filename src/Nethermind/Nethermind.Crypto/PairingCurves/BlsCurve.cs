// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethermind.Int256;

namespace Nethermind.Crypto.PairingCurves;

// BLS12-381 curve
public class BlsCurve
{
    public static readonly BigInteger BaseFieldOrder = new([0x1a, 0x01, 0x11, 0xea, 0x39, 0x7f, 0xe6, 0x9a, 0x4b, 0x1b, 0xa7, 0xb6, 0x43, 0x4b, 0xac, 0xd7, 0x64, 0x77, 0x4b, 0x84, 0xf3, 0x85, 0x12, 0xbf, 0x67, 0x30, 0xd2, 0xa0, 0xf6, 0xb0, 0xf6, 0x24, 0x1e, 0xab, 0xff, 0xfe, 0xb1, 0x53, 0xff, 0xff, 0xb9, 0xfe, 0xff, 0xff, 0xff, 0xff, 0xaa, 0xab], true, true);
    public static readonly BigInteger FqQuadraticNonResidue = BaseFieldOrder - 1;
    public static readonly byte[] SubgroupOrder = [0x73, 0xed, 0xa7, 0x53, 0x29, 0x9d, 0x7d, 0x48, 0x33, 0x39, 0xd8, 0x08, 0x09, 0xa1, 0xd8, 0x05, 0x53, 0xbd, 0xa4, 0x02, 0xff, 0xfe, 0x5b, 0xfe, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x01];
    public static readonly byte[] SubgroupOrderMinusOne = [0x73, 0xed, 0xa7, 0x53, 0x29, 0x9d, 0x7d, 0x48, 0x33, 0x39, 0xd8, 0x08, 0x09, 0xa1, 0xd8, 0x05, 0x53, 0xbd, 0xa4, 0x02, 0xff, 0xfe, 0x5b, 0xfe, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00];

    public static bool PairingVerify(G1 p, G2 q)
    {
        Span<byte> encoded = stackalloc byte[384];
        Span<byte> output = stackalloc byte[32];
        p.Encode(encoded[..128]);
        q.Encode(encoded[128..]);
        Pairings.BlsPairing(encoded, output);
        return output[31] == 1;
    }

    public static bool PairingVerify2(G1 a1, G2 a2, G1 b1, G2 b2)
    {
        Span<byte> encoded = stackalloc byte[384 * 2];
        Span<byte> output = stackalloc byte[32];
        a1.Encode(encoded[..128]);
        a2.Encode(encoded[128..384]);
        b1.Encode(encoded[384..512]);
        b2.Encode(encoded[512..]);
        Pairings.BlsPairing(encoded, output);
        return output[31] == 1;
    }

    public static bool PairingsEqual(G1 a1, G2 a2, G1 b1, G2 b2)
    {
        return PairingVerify2(-a1, a2, b1, b2);
    }

    public static GT Pairing(G1 p, G2 q)
    {
        if (p == G1.Zero || q == G2.Zero || !p.IsInSubgroup() || !q.IsInSubgroup())
        {
            return new(null);
        }

        Fq12<BaseField>? r = FieldArithmetic<BaseField>.Pairing(p.ToFq()!.Value, q.ToFq()!.Value, Params.Instance);
        return new(r);
    }

    public class BaseField : IBaseField
    {
        public static readonly BaseField Instance = new();
        private BaseField() { }

        public BigInteger GetOrder()
        {
            return BaseFieldOrder;
        }

        public int GetSize()
        {
            return 48;
        }
    }

    public class Params : ICurveParams<BaseField>
    {
        public static readonly Params Instance = new();
        private Params() { }

        public (Fq<BaseField>, Fq<BaseField>) G1FromX(Fq<BaseField> x, bool sign)
        {
            (Fq<BaseField>, Fq<BaseField>) res = Fq<BaseField>.Sqrt((x ^ Fq(3)) + Fq(4));
            return (x, sign ? res.Item1 : res.Item2);
        }

        public bool G1IsOnCurve((Fq<BaseField>, Fq<BaseField>)? p)
        {
            if (p is null)
            {
                return false;
            }
            else
            {
                return (p.Value.Item1 ^ Fq(3)) + Fq(4) == p.Value.Item2 * p.Value.Item2;
            }
        }

        public (Fq2<BaseField>, Fq2<BaseField>) G2FromX(Fq2<BaseField> x, bool sign)
        {
            return (x, Fq2<BaseField>.Sqrt((x ^ Fq(3)) + Fq2<BaseField>.MulNonRes(Fq2(4)), sign));
        }

        public bool G2IsOnCurve((Fq2<BaseField>, Fq2<BaseField>)? p)
        {
            if (p is null)
            {
                return false;
            }
            else
            {
                return (p.Value.Item1 ^ Fq(3)) + Fq2<BaseField>.MulNonRes(Fq2(4)) == p.Value.Item2 * p.Value.Item2;
            }
        }

        public (Fq12<BaseField>, Fq12<BaseField>) GTFromX(Fq12<BaseField> x, bool sign)
        {
            throw new NotImplementedException();
        }

        public bool GTIsOnCurve((Fq12<BaseField>, Fq12<BaseField>)? p)
        {
            if (p is null)
            {
                return false;
            }
            else
            {
                return (p.Value.Item1 ^ Fq(3)) + Fq12<BaseField>.MulNonRes(Fq12(4)) == p.Value.Item2 * p.Value.Item2;
            }
        }

        public BaseField GetBaseField()
        {
            return BaseField.Instance;
        }

        public BigInteger GetSubgroupOrder()
        {
            return new BigInteger(SubgroupOrder, true, true);
        }

        public BigInteger GetX()
        {
            return -new BigInteger(0xd201000000010000);
        }
    }

    public static Fq<BaseField> Fq(ReadOnlySpan<byte> bytes)
    {
        return new(bytes, BaseField.Instance);
    }

    public static Fq<BaseField> Fq(BigInteger x)
    {
        return new(x, BaseField.Instance);
    }

    public static Fq2<BaseField> Fq2(ReadOnlySpan<byte> X0, ReadOnlySpan<byte> X1)
    {
        return new(Fq(X0), Fq(X1), BaseField.Instance);
    }

    public static Fq2<BaseField> Fq2(Fq<BaseField> a, Fq<BaseField> b)
    {
        return new(a, b, BaseField.Instance);
    }

    public static Fq2<BaseField> Fq2(Fq<BaseField> b)
    {
        return Fq2<BaseField>.FromFq(b);
    }

    public static Fq2<BaseField> Fq2(BigInteger b)
    {
        return Fq2<BaseField>.FromFq(Fq(b));
    }

    public static Fq6<BaseField> Fq6(Fq2<BaseField> a, Fq2<BaseField> b, Fq2<BaseField> c)
    {
        return new(a, b, c, BaseField.Instance);
    }

    public static Fq6<BaseField> Fq6(BigInteger b)
    {
        return Fq6<BaseField>.FromFq(Fq(b));
    }

    public static Fq6<BaseField> Fq6(Fq<BaseField> b)
    {
        return Fq6<BaseField>.FromFq(b);
    }

    public static Fq12<BaseField> Fq12(Fq6<BaseField> a, Fq6<BaseField> b)
    {
        return new(a, b, BaseField.Instance);
    }

    public static Fq12<BaseField> Fq12(BigInteger b)
    {
        return Fq12<BaseField>.FromFq(Fq(b));
    }

    public static Fq12<BaseField> Fq12(Fq<BaseField> b)
    {
        return Fq12<BaseField>.FromFq(b);
    }

    public class G1 : IEquatable<G1>
    {
        public readonly byte[] X = new byte[48];
        public readonly byte[] Y = new byte[48];
        public static readonly G1 Zero = new(
            [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]
        );
        public static readonly G1 Generator = new(
            [0x17, 0xF1, 0xD3, 0xA7, 0x31, 0x97, 0xD7, 0x94, 0x26, 0x95, 0x63, 0x8C, 0x4F, 0xA9, 0xAC, 0x0F, 0xC3, 0x68, 0x8C, 0x4F, 0x97, 0x74, 0xB9, 0x05, 0xA1, 0x4E, 0x3A, 0x3F, 0x17, 0x1B, 0xAC, 0x58, 0x6C, 0x55, 0xE8, 0x3F, 0xF9, 0x7A, 0x1A, 0xEF, 0xFB, 0x3A, 0xF0, 0x0A, 0xDB, 0x22, 0xC6, 0xBB],
            [0x08, 0xB3, 0xF4, 0x81, 0xE3, 0xAA, 0xA0, 0xF1, 0xA0, 0x9E, 0x30, 0xED, 0x74, 0x1D, 0x8A, 0xE4, 0xFC, 0xF5, 0xE0, 0x95, 0xD5, 0xD0, 0x0A, 0xF6, 0x00, 0xDB, 0x18, 0xCB, 0x2C, 0x04, 0xB3, 0xED, 0xD0, 0x3C, 0xC7, 0x44, 0xA2, 0x88, 0x8A, 0xE4, 0x0C, 0xAA, 0x23, 0x29, 0x46, 0xC5, 0xE7, 0xE1]
        );

        public G1(ReadOnlySpan<byte> X, ReadOnlySpan<byte> Y)
        {
            if (X.Length != 48 || Y.Length != 48)
            {
                throw new Exception("Cannot create G1 point, encoded X and Y must be 48 bytes each.");
            }
            X.CopyTo(this.X);
            Y.CopyTo(this.Y);
        }

        public G1((Fq<BaseField>, Fq<BaseField>)? p)
        {
            if (Params.Instance.G1IsOnCurve(p))
            {
                X = p.Value.Item1.ToBytes();
                Y = p.Value.Item2.ToBytes();

                if (!IsInSubgroup())
                {
                    throw new Exception("Invalid G1 point");
                }
            }
        }

        public (Fq<BaseField>, Fq<BaseField>)? ToFq()
        {
            if (this == Zero)
            {
                return null;
            }
            else
            {
                return (Fq(X), Fq(Y));
            }
        }

        public static G1 FromX(ReadOnlySpan<byte> X, bool sign)
        {
            (Fq<BaseField>, Fq<BaseField>) res = Fq<BaseField>.Sqrt((Fq(X) ^ Fq(3)) + Fq(4));
            Fq<BaseField> Y = sign ? res.Item1 : res.Item2;
            return new G1(X, Y.ToBytes());
        }

        public static G1 FromScalar(UInt256 x)
        {
            return x.ToBigEndian() * Generator;
        }

        public override bool Equals(object obj) => Equals(obj as G1);

        public bool Equals(G1 p)
        {
            return X.SequenceEqual(p.X) && Y.SequenceEqual(p.Y);
        }
        public override int GetHashCode() => (X, Y).GetHashCode();

        public static bool operator ==(G1 p, G1 q)
        {
            return p.Equals(q);
        }

        public static bool operator !=(G1 p, G1 q) => !p.Equals(q);

        public static G1 operator *(ReadOnlySpan<byte> s, G1 p)
        {
            if (s.Length != 32)
            {
                throw new Exception("Scalar must be 32 bytes to multiply with G1 point.");
            }

            Span<byte> encoded = stackalloc byte[160];
            Span<byte> output = stackalloc byte[128];
            p.Encode(encoded[..128]);
            s.CopyTo(encoded[128..]);
            Pairings.BlsG1Mul(encoded, output);
            return new G1(output[16..64], output[80..]);
        }

        public static G1 operator *(UInt256 s, G1 p)
        {
            return s.ToBigEndian() * p;
        }

        public static G1 operator +(G1 p, G1 q)
        {
            Span<byte> encoded = stackalloc byte[256];
            Span<byte> output = stackalloc byte[128];
            p.Encode(encoded[..128]);
            q.Encode(encoded[128..]);
            Pairings.BlsG1Add(encoded, output);
            return new G1(output[16..64], output[80..]);
        }

        public static G1 MapToCurve(byte[] Fq)
        {
            Span<byte> encoded = stackalloc byte[64];
            Span<byte> output = stackalloc byte[128];
            Fq.CopyTo(encoded[16..]);
            Pairings.BlsMapToG1(encoded, output);
            return new G1(output[16..64], output[80..]);
        }

        public static G1 operator -(G1 p)
        {
            if (p == Zero)
            {
                return Zero;
            }

            (Fq<BaseField>, Fq<BaseField>)? tmp = p.ToFq();
            return new((tmp.Value.Item1, -tmp.Value.Item2));
        }

        public bool IsInSubgroup()
        {
            return (SubgroupOrder * this) == Zero;
        }

        internal void Encode(Span<byte> output)
        {
            if (output.Length != 128)
            {
                throw new Exception("Encoding G1 point requires 128 bytes.");
            }

            X.CopyTo(output[16..]);
            Y.CopyTo(output[80..]);
        }
    }

    public class G2 : IEquatable<G2>
    {
        public readonly (byte[], byte[]) X = (new byte[48], new byte[48]);
        public readonly (byte[], byte[]) Y = (new byte[48], new byte[48]);
        public static readonly G2 Zero = new(
            [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]
        );
        public static readonly G2 Generator = new(
            [0x02, 0x4a, 0xa2, 0xb2, 0xf0, 0x8f, 0x0a, 0x91, 0x26, 0x08, 0x05, 0x27, 0x2d, 0xc5, 0x10, 0x51, 0xc6, 0xe4, 0x7a, 0xd4, 0xfa, 0x40, 0x3b, 0x02, 0xb4, 0x51, 0x0b, 0x64, 0x7a, 0xe3, 0xd1, 0x77, 0x0b, 0xac, 0x03, 0x26, 0xa8, 0x05, 0xbb, 0xef, 0xd4, 0x80, 0x56, 0xc8, 0xc1, 0x21, 0xbd, 0xb8],
            [0x13, 0xe0, 0x2b, 0x60, 0x52, 0x71, 0x9f, 0x60, 0x7d, 0xac, 0xd3, 0xa0, 0x88, 0x27, 0x4f, 0x65, 0x59, 0x6b, 0xd0, 0xd0, 0x99, 0x20, 0xb6, 0x1a, 0xb5, 0xda, 0x61, 0xbb, 0xdc, 0x7f, 0x50, 0x49, 0x33, 0x4c, 0xf1, 0x12, 0x13, 0x94, 0x5d, 0x57, 0xe5, 0xac, 0x7d, 0x05, 0x5d, 0x04, 0x2b, 0x7e],
            [0x0c, 0xe5, 0xd5, 0x27, 0x72, 0x7d, 0x6e, 0x11, 0x8c, 0xc9, 0xcd, 0xc6, 0xda, 0x2e, 0x35, 0x1a, 0xad, 0xfd, 0x9b, 0xaa, 0x8c, 0xbd, 0xd3, 0xa7, 0x6d, 0x42, 0x9a, 0x69, 0x51, 0x60, 0xd1, 0x2c, 0x92, 0x3a, 0xc9, 0xcc, 0x3b, 0xac, 0xa2, 0x89, 0xe1, 0x93, 0x54, 0x86, 0x08, 0xb8, 0x28, 0x01],
            [0x06, 0x06, 0xc4, 0xa0, 0x2e, 0xa7, 0x34, 0xcc, 0x32, 0xac, 0xd2, 0xb0, 0x2b, 0xc2, 0x8b, 0x99, 0xcb, 0x3e, 0x28, 0x7e, 0x85, 0xa7, 0x63, 0xaf, 0x26, 0x74, 0x92, 0xab, 0x57, 0x2e, 0x99, 0xab, 0x3f, 0x37, 0x0d, 0x27, 0x5c, 0xec, 0x1d, 0xa1, 0xaa, 0xa9, 0x07, 0x5f, 0xf0, 0x5f, 0x79, 0xbe]
        );

        public G2(ReadOnlySpan<byte> X0, ReadOnlySpan<byte> X1, ReadOnlySpan<byte> Y0, ReadOnlySpan<byte> Y1)
        {
            if (X0.Length != 48 || X1.Length != 48 || Y0.Length != 48 || Y1.Length != 48)
            {
                throw new Exception("Cannot create G2 point, encoded values must be 48 bytes each.");
            }
            X0.CopyTo(X.Item1);
            X1.CopyTo(X.Item2);
            Y0.CopyTo(Y.Item1);
            Y1.CopyTo(Y.Item2);
        }

        public G2((Fq2<BaseField>, Fq2<BaseField>)? p)
        {
            if (Params.Instance.G2IsOnCurve(p))
            {
                X.Item1 = p.Value.Item1.b.ToBytes();
                X.Item2 = p.Value.Item1.a.ToBytes();
                Y.Item1 = p.Value.Item2.b.ToBytes();
                Y.Item2 = p.Value.Item2.a.ToBytes();

                if (!IsInSubgroup())
                {
                    throw new Exception("Invalid G2 point");
                }
            }
        }

        public static G2 FromScalar(UInt256 x)
        {
            return x.ToBigEndian() * Generator;
        }

        public static G2 FromX(ReadOnlySpan<byte> X0, ReadOnlySpan<byte> X1, bool sign)
        {
            if (X0.Length != 48 || X1.Length != 48)
            {
                throw new Exception("Cannot create G2 point from X, encoded values must be 48 bytes each.");
            }

            Fq2<BaseField> y = Params.Instance.G2FromX(Fq2(X1, X0), sign).Item2;
            return new G2(X0, X1, y.b.ToBytes(), y.a.ToBytes());
        }

        public (Fq2<BaseField>, Fq2<BaseField>)? ToFq()
        {
            if (this == Zero)
            {
                return null;
            }
            else
            {
                return (Fq2(X.Item2, X.Item1), Fq2(Y.Item2, Y.Item1));
            }
        }

        public override bool Equals(object obj) => Equals(obj as G1);

        public bool Equals(G2 p)
        {
            return X.Item1.SequenceEqual(p.X.Item1) && X.Item2.SequenceEqual(p.X.Item2) && Y.Item1.SequenceEqual(p.Y.Item1) && Y.Item2.SequenceEqual(p.Y.Item2);
        }
        public override int GetHashCode() => (X, Y).GetHashCode();

        public static bool operator ==(G2 p, G2 q)
        {
            return p.Equals(q);
        }

        public static bool operator !=(G2 p, G2 q) => !p.Equals(q);

        public static G2 operator *(ReadOnlySpan<byte> s, G2 p)
        {
            if (s.Length != 32)
            {
                throw new Exception("Scalar must be 32 bytes to multiply with G2 point.");
            }

            Span<byte> encoded = stackalloc byte[288];
            Span<byte> output = stackalloc byte[256];
            p.Encode(encoded[..256]);
            s.CopyTo(encoded[256..]);
            Pairings.BlsG2Mul(encoded, output);
            return new G2(output[16..64], output[80..128], output[144..192], output[208..]);
        }

        public static G2 operator *(UInt256 s, G2 p)
        {
            return s.ToBigEndian() * p;
        }

        public static G2 operator +(G2 p, G2 q)
        {
            Span<byte> encoded = stackalloc byte[512];
            Span<byte> output = stackalloc byte[256];
            p.Encode(encoded[..256]);
            q.Encode(encoded[256..]);
            Pairings.BlsG2Add(encoded, output);
            return new G2(output[16..64], output[80..128], output[144..192], output[208..]);
        }

        public static G2 operator -(G2 p)
        {
            if (p == Zero)
            {
                return Zero;
            }

            (Fq2<BaseField>, Fq2<BaseField>)? tmp = p.ToFq();
            return new((tmp.Value.Item1, -tmp.Value.Item2));
        }

        public bool IsInSubgroup()
        {
            return (SubgroupOrder * this) == Zero;
        }

        internal void Encode(Span<byte> output)
        {
            if (output.Length != 256)
            {
                throw new Exception("Encoding G2 point requires 256 bytes.");
            }

            X.Item1.CopyTo(output[16..]);
            X.Item2.CopyTo(output[80..]);
            Y.Item1.CopyTo(output[144..]);
            Y.Item2.CopyTo(output[208..]);
        }
    }

    public class GT(Fq12<BaseField>? x) : IEquatable<GT>
    {
        public readonly Fq12<BaseField>? X = x;

        public static GT operator *(UInt256 s, GT p)
        {
            throw new NotImplementedException();
        }
        public override bool Equals(object obj) => Equals(obj as GT);

        public bool Equals(GT p)
        {
            return X == p.X;
        }
        public override int GetHashCode() => X.GetHashCode();

        public static bool operator ==(GT x, GT y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(GT x, GT y) => !x.Equals(y);
    }
}
