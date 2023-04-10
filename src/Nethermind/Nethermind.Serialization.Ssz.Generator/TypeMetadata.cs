// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using SourceGenerator;

namespace Nethermind.Serialization.Ssz.Generator;

public enum SszElementKind
{
    Primitive, Container, Vector, List, BitVector, BitList
}

public enum DeclarationType
{
    Class = 0, Struct = 1, Record = 2
}

public class TypeMetadata
{
    public ISymbol Symbol { get; private set; }
    public string? Name { get; set; }
    public string TypeName { get; set; }
    public bool IsCollection { get; set; }
    public INamedTypeSymbol TypeSymbol { get; set; }
    public SszElementKind Kind { get; set; }
    public TypeMetadata[] Members { get; set; }
    public DeclarationType DeclarationType { get; set; }
    public bool IsFixedLength { get; set; }
    public int StaticOffset { get; set; }

    public TypeMetadata(IPropertySymbol typeDecl)
    {
        Name = typeDecl.Name;
        if (SpecialType.System_Collections_Generic_IEnumerable_T == TypeSymbol.SpecialType)
        {
            IsCollection = true;
            TypeSymbol = (INamedTypeSymbol)TypeSymbol.TypeArguments[0];
        }
        else
        {
            TypeSymbol = (INamedTypeSymbol)typeDecl.Type;
        }

        SetMembers();
        SetKind();
        CalculateStaticOffset();
    }
    public TypeMetadata(INamedTypeSymbol typeDecl)
    {
        DeclarationType = (DeclarationType)((typeDecl.IsRecord ? 2 : 0) + (typeDecl.IsValueType ? 1 : 0));
        Symbol = typeDecl;
        TypeSymbol = typeDecl;
        TypeName = typeDecl.Name;

        SetMembers();
        SetKind();
        CalculateStaticOffset();
    }

    private void SetMembers()
    {
        Members = TypeSymbol.GetAllMembers() // iterate includes parent type
            .Where(x => x is IPropertySymbol and { IsStatic: false, IsImplicitlyDeclared: false, CanBeReferencedByName: true })
            .Where(x =>
            {
                var include = x.ContainsAttribute("IncludeFieldAttribute");
                var ignore = x.ContainsAttribute("IgnoreFieldAttribute");
                if (ignore) return false;
                if (include) return true;
                return x.DeclaredAccessibility is Accessibility.Public;
            })
            .Where(x =>
            {
                var p = x as IPropertySymbol;

                // set only can't be serializable member
                if ((p.GetMethod == null && p.SetMethod != null) || p.IsIndexer)
                {
                    return false;
                }
                return true;
            })
            .Select(x =>
            {
                return new TypeMetadata(x as IPropertySymbol);
            })
            .ToArray();
    }
    private void SetKind()
    {
        Kind = this.IsPrimitive(out _)
            ? SszElementKind.Primitive
            : this.IsList()
                ? SszElementKind.List
                : this.IsVector()
                    ? SszElementKind.Vector
                    : this.IsBitList()
                        ? SszElementKind.BitList
                        : this.IsBitVector()
                            ? SszElementKind.BitVector
                            : SszElementKind.Container;
    }

    internal void GenerateCode(StringBuilder sb, SourceProductionContext context)
    {
        var classOrStructOrRecord = (DeclarationType.HasFlag(DeclarationType.Record), DeclarationType.HasFlag(DeclarationType.Struct)) switch
        {
            (true, true) => "record struct",
            (true, false) => "record",
            (false, true) => "struct",
            (false, false) => "class",
        };

        sb.AppendLine($$"""
            partial {{classOrStructOrRecord}} {{TypeName}}
            {
                public static int  SszSizeOf{{TypeName}}  => {{StaticOffset}};
                public static bool SszIs{{TypeName}}Fixed => {{IsFixedLength}}; 

                public static {{TypeName}} Decode(Span<byte> span)
                {
                    if(Encode(span, out {{TypeName}} result)) {
                        return result;
                    } else {
                        throw new Exception("Decoding Failed");
                    }
                }
                private static bool TryDecode(Span<byte> span, [NotNullWhenTrue] out {{TypeName}}? value)
                {
                    {{EmitDecoderBody()}}
                }


                public static void Encode(Span<byte> span, scoped ref {{TypeName}}? value)
                {
                    int i = 0;
                    return Encode(span, scoped ref value, ref i);
                }
                private static void Encode(Span<byte> span, scoped ref {{TypeName}}? container, ref int offset)
                {
                    {{EmitEncoderBody()}}
                }
            }
            """);
    }

    private string EmitEncoderBody()
    {
        StringBuilder sb = new();
        Queue<TypeMetadata> q = new();
        foreach (var member in Members)
        {
            switch (member.Kind)
            {
                case SszElementKind.Container:
                    sb.Append($"Encode(span, {StaticOffset}, ref offset);");
                    q.Enqueue(member);
                    break;
                case SszElementKind.Vector:
                    sb.Append($$"""
                            foreach(var item in {{member.Name}}) {
                                Encode(span, item, ref offset);
                            }
                        """);
                    break;
                case SszElementKind.List:
                    sb.Append($"Encode(span, {StaticOffset}, ref offset);");
                    q.Enqueue(member);
                    break;
                case SszElementKind.BitVector:
                    sb.Append($$"""
                            foreach(var item in {{member.Name}}) {
                                Encode(span, item, ref offset);
                            }
                        """);
                    break;
                case SszElementKind.BitList:
                    sb.Append($$"""
                            foreach(var item in {{member.Name}}) {
                                Encode(span, item, ref offset);
                            }
                        """);
                    break;
            }
        }


        while (q.Count > 0)
        {
            var item = q.Dequeue();
            sb.Append($$"""
                Encode(span, {{item.Name}}, ref offset(;
                """);
        }

        return sb.ToString();
    }

    private object EmitDecoderBody()
    {
        throw new NotImplementedException();
    }

    private void CalculateStaticOffset()
    {
        int offset = 0, offsetLen = 32;
        bool isFixedLength = true;
        foreach (var member in Members)
        {
            switch (member.Kind)
            {
                case SszElementKind.Primitive:
                    string name = (member.Symbol as IPropertySymbol).Name;
                    int size = name switch
                    {
                        "uint8" => 8,
                        "uint16" => 16,
                        "uint32" => 32,
                        "uint64" => 64,
                        "uint128" => 128,
                        "uint256" => 256,
                        "bool" => 1,
                    };
                    offset += size;
                    break;
                default:
                    if (IsCollection)
                    {
                        var sizeofAttr = Symbol.GetAttribute("SizeOfAttribute");
                        if (sizeofAttr is not null)
                        {
                            var itemsCounts = (int)sizeofAttr.ConstructorArguments[0].Value;
                            // check if element is fixedSize or Dynamic
                            if (member.IsFixedLength)
                            {
                                offset += member.StaticOffset * itemsCounts;
                            }
                            else
                            {
                                offset += offsetLen * itemsCounts;
                            }
                        }
                        else
                        {
                            offset += offsetLen;
                        }
                        isFixedLength &= member.IsFixedLength;
                        break;
                    }
                    else if (member.Kind is SszElementKind.Container)
                    {
                        isFixedLength &= member.IsFixedLength;

                        offset += 64;
                    }
                    break;
            }
        }
        StaticOffset = offset;
        IsFixedLength = isFixedLength;
    }
}
