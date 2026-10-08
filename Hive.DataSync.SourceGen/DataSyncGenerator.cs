using System;
using System.Globalization;
using System.Linq;
using Corona.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hive.DataSync.SourceGen;

[Generator]
public sealed class DataSyncGenerator : IIncrementalGenerator
{
    private const string AttributePrefix = "Hive.DataSync.Shared.Attributes.";
    private static readonly DiagnosticDescriptor InvalidTarget = new("HIVESYNC001", "Invalid sync object",
        "Cannot generate sync object: {0}", "Hive.DataSync", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var results = context.SyntaxProvider.ForAttributeWithMetadataName(AttributePrefix + "SyncObjectAttribute",
            static (node, _) => node is ClassDeclarationSyntax, static (ctx, token) =>
            {
                var type = (INamedTypeSymbol)ctx.TargetSymbol;
                for (var current = type; current is not null; current = current.ContainingType)
                    if (!current.IsPartial(token)) return Error("target and containing types must be partial");
                if (type.IsStatic) return Error("target must be an instance class");
                var fields = new System.Collections.Generic.List<SyncField>();
                var generatedNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(static f => f.HasAttribute(AttributePrefix + "SyncPropertyAttribute")))
                {
                    token.ThrowIfCancellationRequested();
                    if (field.IsStatic || field.IsReadOnly || field.IsConst) return Error("sync field must be writable: " + field.Name);
                    var property = CSharpNames.PropertyName(field.Name);
                    if (!CSharpNames.IsIdentifier(property) || property == field.Name || type.GetMembers(property).Length > 0 || !generatedNames.Add(property))
                        return Error("invalid or duplicate generated property: " + property);
                    var serializer = field.GetAttributes().FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == AttributePrefix + "CustomSerializerAttribute");
                    var packet = serializer?.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol custom
                        ? custom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : PacketType(field.Type.SpecialType);
                    if (packet is null) return Error("unsupported sync field type; specify CustomSerializer: " + field.Name);
                    var option = field.GetAttributes().FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == AttributePrefix + "SyncOptionAttribute");
                    var options = option is null ? "global::Hive.Common.Shared.SyncOptions.ClientOnly" :
                        "(global::Hive.Common.Shared.SyncOptions)" + Convert.ToString(option.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
                    fields.Add(new(CSharpNames.Identifier(field.Name), CSharpNames.Identifier(property),
                        field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), packet, options));
                }
                var id = Convert.ToString(ctx.Attributes[0].ConstructorArguments[0].Value, CultureInfo.InvariantCulture) ?? "0";
                return new GenerationResult<SyncModel>(new(TypeDeclarationModel.From(type), id,
                    type.GetMembers("ObjectSyncId").Length == 0, new(fields.OrderBy(static f => f.Property, StringComparer.Ordinal))), default);

                GenerationResult<SyncModel> Error(string message) => new(null,
                    new(new[] { DiagnosticInfo.Create(InvalidTarget, ctx.TargetNode.GetLocation(), message) }));
            });
        context.RegisterSourceOutput(results.SelectMany(static (result, _) => result.Diagnostics),
            static (output, diagnostic) => output.ReportDiagnostic(diagnostic.ToDiagnostic()));
        var models = results.Where(static result => result.Value is not null).Select(static (result, _) => result.Value!)
            .WithTrackingName("SyncModels");
        context.RegisterSourceOutput(models, static (output, model) => output.AddSource(SyncEmitter.Emit(model)));
    }

    private static string? PacketType(SpecialType type)
    {
        var name = type switch
        {
            SpecialType.System_Boolean => "Boolean", SpecialType.System_Char => "Char",
            SpecialType.System_Double => "Double", SpecialType.System_Int16 => "Int16", SpecialType.System_Int32 => "Int32",
            SpecialType.System_Int64 => "Int64", SpecialType.System_Single => "Single", SpecialType.System_String => "String",
            SpecialType.System_UInt16 => "UInt16", SpecialType.System_UInt32 => "UInt32", SpecialType.System_UInt64 => "UInt64", _ => null
        };
        return name is null ? null : "global::Hive.DataSync.Shared.ObjectSyncPacket." + name + "SyncPacket";
    }
}

internal sealed record SyncField(string Field, string Property, string Type, string Packet, string Options);
internal sealed record SyncModel(TypeDeclarationModel Type, string Id, bool GenerateId, EquatableArray<SyncField> Fields);
