// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Nethermind.Serialization.Ssz.Generator;

namespace SourceGenerator
{
    public static class TypeExtentions
    {
        internal static void GetAllTypeParameters(this INamedTypeSymbol type, List<ITypeParameterSymbol> result)
        {
            var containingType = type.ContainingType;
            if ((object)containingType != null)
            {
                containingType.GetAllTypeParameters(result);
            }

            result.AddRange(type.TypeParameters);
        }
        public static IEnumerable<ISymbol> GetAllMembers(this ITypeSymbol symbol, bool withoutOverride = true)
        {
            // Iterate Parent -> Derived
            if (symbol.BaseType != null)
            {
                foreach (var item in GetAllMembers(symbol.BaseType))
                {
                    // override item already iterated in parent type
                    if (!withoutOverride || !item.IsOverride)
                    {
                        yield return item;
                    }
                }
            }

            foreach (var item in symbol.GetMembers())
            {
                if (!withoutOverride || !item.IsOverride)
                {
                    yield return item;
                }
            }
        }
        public static AttributeData? GetAttribute(this ISymbol symbol, string attribtueName)
        {
            return symbol.GetAttributes().FirstOrDefault(x => x.AttributeClass.Name == attribtueName);
        }
        public static bool ContainsAttribute(this ISymbol symbol, string attribtueName)
        {
            return GetAttribute(symbol, attribtueName) is not null;
        }
        public static bool IsPrimitive(this TypeMetadata type, out bool isBoolean)
        {
            isBoolean = false;
            switch (type.TypeName)
            {
                case "uint8":
                case "uint16":
                case "uint32":
                case "uint64":
                case "uint128":
                case "uint256":
                    return true;
                case "bool":
                    {
                        isBoolean = true;
                        return isBoolean;
                    }
            };
            return false;
        }
        public static bool IsVector(this TypeMetadata type)
        {

            return false;
        }
        public static bool IsList(this TypeMetadata type)
        {

            return false;
        }
        public static bool IsBitVector(this TypeMetadata type)
        {

            return false;
        }
        public static bool IsBitList(this TypeMetadata type)
        {

            return false;
        }
        public static bool IsUnion(this TypeMetadata type)
        {
            return type.TypeSymbol.TypeArgumentNullableAnnotations.Any();
        }
    }
}
