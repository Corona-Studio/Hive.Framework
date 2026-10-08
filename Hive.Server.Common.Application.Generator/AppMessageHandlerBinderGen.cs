using System;
using System.Linq;
using Corona.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hive.Server.Common.Application.SourceGen;

[Generator]
public sealed class AppMessageHandlerBinderGen : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidHandler = new("HIVEAPP001", "Invalid application message handler",
        "Handler '{0}' must be an accessible instance method in a top-level non-generic ServerApplicationBase, accept a request or MessageContext<T> (and optional ISession), and return ValueTask<ResultContext<T>>",
        "Hive.Application", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var results = context.SyntaxProvider.ForAttributeWithMetadataName("Hive.Server.Common.Application.MessageHandlerAttribute",
            static (node, _) => node is MethodDeclarationSyntax, static (ctx, token) =>
            {
                token.ThrowIfCancellationRequested();
                var method = (IMethodSymbol)ctx.TargetSymbol;
                var type = method.ContainingType;
                var identity = CSharpNames.MetadataName(type);
                if (type.ContainingType is not null || type.Arity != 0 || !type.InheritsFrom("Hive.Server.Common.Application.ServerApplicationBase") ||
                    method.IsStatic || method.Arity != 0 || method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal) ||
                    method.Parameters.Length is < 1 or > 2 || method.Parameters.Any(static p => p.RefKind != RefKind.None) ||
                    (method.Parameters.Length == 2 && method.Parameters[1].Type.ToDisplayString() != "Hive.Network.Abstractions.Session.ISession") ||
                    method.ReturnType is not INamedTypeSymbol { Name: "ValueTask", Arity: 1 } valueTask ||
                    valueTask.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks" ||
                    valueTask.TypeArguments[0] is not INamedTypeSymbol { Name: "ResultContext", Arity: 1 } result ||
                    result.ContainingNamespace.ToDisplayString() != "Hive.Both.General.Dispatchers")
                    return new BinderResult(identity, null, DiagnosticInfo.Create(InvalidHandler, method.Locations.FirstOrDefault(), method.Name));
                var request = method.Parameters[0].Type;
                if (request is INamedTypeSymbol { Name: "MessageContext", Arity: 1 } message &&
                    message.ContainingNamespace.ToDisplayString() == "Hive.Both.General.Dispatchers")
                {
                    if (method.Parameters.Length != 1) return new BinderResult(identity, null,
                        DiagnosticInfo.Create(InvalidHandler, method.Locations.FirstOrDefault(), method.Name));
                    request = message.TypeArguments[0];
                }
                return new BinderResult(identity, new(CSharpNames.Namespace(type.ContainingNamespace),
                    CSharpNames.Identifier(type.Name + "HandlerBinder"), type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    CSharpNames.Identifier(method.Name), request.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    result.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)), null);
            });
        context.RegisterSourceOutput(results.Where(static result => result.Error is not null),
            static (output, result) => output.ReportDiagnostic(result.Error!.ToDiagnostic()));
        var models = results.Collect().SelectMany(static (items, _) => items.GroupBy(static item => item.Identity)
            .Where(static group => group.All(static item => item.Method is not null)).OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new BinderModel(group.Key, new(group.Select(static item => item.Method!)
                .OrderBy(static method => method.Name, StringComparer.Ordinal).ThenBy(static method => method.Request, StringComparer.Ordinal))))
            .ToArray()).WithTrackingName("BinderModels");
        context.RegisterSourceOutput(models, static (output, model) => output.AddSource(BinderEmitter.Emit(model)));
    }
}

internal sealed record BinderResult(string Identity, BinderMethod? Method, DiagnosticInfo? Error);
internal sealed record BinderMethod(string Namespace, string BinderName, string Owner, string Name, string Request, string Reply);
internal sealed record BinderModel(string Identity, EquatableArray<BinderMethod> Methods);
