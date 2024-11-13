#define GENERATE_BY_TEMPLATE

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hive.Server.Common.Application.SourceGen
{
    [Generator]
    public class AppMessageHandlerBinderGen : IIncrementalGenerator
    {
        private static DiagnosticDescriptor _descriptor = new DiagnosticDescriptor("Hive0001", "Hive0001", "Hive0001", "Hive",
            DiagnosticSeverity.Error, true);
        
        
        private const string Template = """

{0}

namespace {1}
{{
    public class {2}HandlerBinder : IMessageHandlerBinder
    {{
        public Task BindAndStart(ServerApplicationBase appBase, IDispatcher dispatcher, CancellationToken stoppingToken)
        {{
            IMessageHandlerBinder binder = this;
            {3} app = ({3})appBase;
            List<Task> taskList = new List<Task>
            {{
                {4}
            }};
            return Task.WhenAll(taskList);
        }}
    }}
}}
                             
""";
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (s, _) => s is ClassDeclarationSyntax,
                    static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                .Where(classDecl => classDecl.AttributeLists.Count > 0);

            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
            {
                var (compilation, sourceClassDeclarations) = source;

                var serverAppBaseType = compilation.GetTypeByMetadataName("Hive.Server.Common.Application.ServerApplicationBase");

                foreach (var classDecl in sourceClassDeclarations)
                {
                    var model = compilation.GetSemanticModel(classDecl.SyntaxTree);
                    var classSymbol = model.GetDeclaredSymbol(classDecl);

                    if (classSymbol == null || !InheritsFrom(classSymbol, serverAppBaseType))
                    {
                        continue;
                    }

                    var handlerMethods = GetHandlerMethods(classDecl, model);
                    if (handlerMethods.Count > 0)
                    {
                        GenerateUsingTemplate(spc, model, classSymbol, handlerMethods);
                    }
                }
            });
        }
        
        private static bool InheritsFrom(ITypeSymbol symbol, ITypeSymbol type)
        {
            var baseType = symbol.BaseType;
            while (baseType != null)
            {
                if (type.Equals(baseType))
                    return true;

                baseType = baseType.BaseType;
            }

            return false;
        }
        
        private static List<MethodDeclarationSyntax> GetHandlerMethods(ClassDeclarationSyntax classDecl, SemanticModel model)
        {
            return classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(method =>
                    method.AttributeLists.Any(attrList =>
                        attrList.Attributes.Any(attr =>
                        {
                            var typeInfo = model.GetTypeInfo(attr).Type;
                            return typeInfo?.Name == "MessageHandlerAttribute";
                        })))
                .ToList();
        }

        private static void GenerateUsingTemplate(SourceProductionContext context,
            SemanticModel model, INamedTypeSymbol classSymbol, List<MethodDeclarationSyntax> handlerMethods)
        {
            var usingBuilder = new StringBuilder()
                .AppendLine("using Hive.Server.Common.Application;")
                .AppendLine("using Hive.Both.General.Dispatchers;")
                .AppendLine("using Microsoft.Extensions.Logging;");

            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
            var binderInvokeList = new List<string>();

            foreach (var handlerMethod in handlerMethods)
            {
                var methodName = handlerMethod.Identifier.Text;
                var requestType = model.GetTypeInfo(handlerMethod.ParameterList.Parameters[0].Type).Type;
                var responseType = model.GetTypeInfo(handlerMethod.ReturnType).Type;

                if (requestType != null && responseType != null)
                {
                    binderInvokeList.Add(
                        $"appBase.StartMessageProcessLoop<{requestType.ToDisplayString()},{responseType.ToDisplayString()}>(dispatcher, app.{methodName}, stoppingToken)");
                }
            }

            var binderInvoke = string.Join(",\n", binderInvokeList);
            var generatedCode = string.Format(
                Template,
                usingBuilder,
                namespaceName,
                classSymbol.Name,
                classSymbol.ToDisplayString(),
                binderInvoke);

            context.AddSource($"{classSymbol.Name}HandlerBinder.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
        }
    }
}