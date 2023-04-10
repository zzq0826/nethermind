// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator
{
    [Generator]
    public class SszGenerator : IIncrementalGenerator
    {
        private static readonly string _filterName = "GenerateSszAttribute";

        public void Execute(IncrementalGeneratorInitializationContext context)
        {
            var encoderGenerator = new EncoderGenerator();
            var decoderGenerator = new DecoderGenerator();
            var targetTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                _filterName,
                predicate: static (node, token) =>
                {
                    // search [MemoryPackable] class or struct or interface or record
                    return (node is ClassDeclarationSyntax
                                 or StructDeclarationSyntax
                                 or RecordDeclarationSyntax);
                },
                transform: static (context, token) =>
                {
                    return (TypeDeclarationSyntax)context.TargetNode;
                });

            var source = targetTypes
                .Combine(context.CompilationProvider);

            context.RegisterSourceOutput(source, (context, source) =>
            {
                var (typeDeclaration, compilation) = source;
                encoderGenerator.EmitEncoderForType(context, compilation, typeDeclaration);
            });

        }
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}

