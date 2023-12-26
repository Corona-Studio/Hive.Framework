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
    public class AppMessageHandlerBinderGen : ISourceGenerator
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
        
        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            //if (!Debugger.IsAttached) Debugger.Launch();
#endif
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

        public void Execute(GeneratorExecutionContext context)
        {
            var syntaxTrees = context.Compilation.SyntaxTrees;
            var serverAppBaseType = context.Compilation.GetTypeByMetadataName("Hive.Server.Common.Application.ServerApplicationBase");
            var messageHandlerType = context.Compilation.GetTypeByMetadataName("Hive.Server.Common.Application.IMessageHandlerBinder");
            
            foreach (var syntaxTree in syntaxTrees)
            {
                var model = context.Compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();


                // Find all classes with [DataSynchronizationObject] attribute
                var appSyntaxList = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(cls => InheritsFrom(model.GetDeclaredSymbol(cls),serverAppBaseType));
               
                foreach (var classSyntax in appSyntaxList)
                {

                    var handlerMethods = new List<MethodDeclarationSyntax>();
                    foreach (var member in classSyntax.Members)
                    {
                        if (!(member is MethodDeclarationSyntax memberDeclarationSyntax)) continue;
                        
                        if (memberDeclarationSyntax.AttributeLists.Any(attSyntax =>
                            {
                                return attSyntax.Attributes.Any(attr =>
                                {
                                    var type = model.GetTypeInfo(attr).Type;
                                    return type?.Name == "MessageHandlerAttribute";
                                });
                            }))
                        {
                            handlerMethods.Add(memberDeclarationSyntax);
                        }
                    }
                    

                    if (handlerMethods.Count == 0)
                    {
                        continue;
                    }


                    var definedApplicationClassSymbol = model.GetDeclaredSymbol(classSyntax);

#if GENERATE_BY_TEMPLATE
                    GenerateUsingTemplate(context, model, definedApplicationClassSymbol, handlerMethods);
#else

                    GenerateUsingSyntaxFactory(context, classSyntax, model, definedApplicationClassSymbol, messageHandlerType, handlerMethods);
#endif
                }
            }
        }

        private static void GenerateUsingTemplate(GeneratorExecutionContext context,
            SemanticModel model, ISymbol definedApplicationClassSymbol, List<MethodDeclarationSyntax> handlerMethods)
        {
            var usingBuilder = new StringBuilder();

            usingBuilder.AppendLine("using Hive.Server.Common.Application;");
            usingBuilder.AppendLine("using Hive.Both.General.Dispatchers;");
            usingBuilder.AppendLine("using Microsoft.Extensions.Logging;");

            var namespaceName = definedApplicationClassSymbol.ContainingNamespace.ToDisplayString();
            var binderInvokeList = new List<string>();
            foreach (var handlerMethod in handlerMethods)
            {
                var methodName = handlerMethod.Identifier.Text;
                var firstParamTypeSyntax = handlerMethod.ParameterList.Parameters[0].Type;
                
                if(firstParamTypeSyntax==null)
                    continue;
                
                if (firstParamTypeSyntax is GenericNameSyntax firstParamGenericNameSyntax)
                {
                    firstParamTypeSyntax = firstParamGenericNameSyntax.TypeArgumentList.Arguments[0];
                }

                var requestType = model.GetTypeInfo(firstParamTypeSyntax).Type;
                var responseType = model.GetTypeInfo(handlerMethod.ReturnType).Type;

                if (handlerMethod.ReturnType is GenericNameSyntax genericNameSyntax) // ValueTask<?>
                {
                    if (genericNameSyntax.TypeArgumentList
                            .Arguments[0] is GenericNameSyntax genericNameSyntax2) // ValueTask<ResultContext<?>>
                    {
                        responseType = model.GetTypeInfo(genericNameSyntax2.TypeArgumentList.Arguments[0]).Type;
                    }
                    else
                    {
                        responseType = model.GetTypeInfo(genericNameSyntax.TypeArgumentList.Arguments[0]).Type;
                    }
                }
                
                /*if(requestType!=null)
                    usingBuilder.AppendLine($"using {requestType.ContainingNamespace.ToDisplayString()};");
                
                if(responseType!=null)
                    usingBuilder.AppendLine($"using {responseType.ContainingNamespace.ToDisplayString()};");*/
                
                if(requestType!=null && responseType!=null)
                    binderInvokeList.Add(
                        $"appBase.StartMessageProcessLoop<{requestType.ToDisplayString()},{responseType.ToDisplayString()}>(dispatcher, app.{methodName}, stoppingToken)");
            }

            var binderInvoke = string.Join(",\n", binderInvokeList);

            var finalCode = string.Format(Template, usingBuilder.ToString(), namespaceName, definedApplicationClassSymbol.Name, definedApplicationClassSymbol.ToDisplayString(), binderInvoke);

            var newSourceText = CSharpSyntaxTree.ParseText(SourceText.From(finalCode, Encoding.UTF8)).GetRoot()
                .NormalizeWhitespace().SyntaxTree.GetText();

            context.AddSource($"{definedApplicationClassSymbol.Name}HandlerBinder.g.cs", newSourceText);
        }

        private static void GenerateUsingSyntaxFactory(GeneratorExecutionContext context, ClassDeclarationSyntax classSyntax,
            SemanticModel model, ISymbol definedApplicationClassSymbol, INamedTypeSymbol messageHandlerType, List<MethodDeclarationSyntax> handlerMethods)
        {
            string newClassName = $"{classSyntax.Identifier.Text}HandlerBinder";

            var usings = new List<UsingDirectiveSyntax>();
            if (definedApplicationClassSymbol != null)
            {
                usings.Add(UsingDirective(IdentifierName(definedApplicationClassSymbol.ContainingNamespace.ToDisplayString()))
                    .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }

            usings.Add(UsingDirective(IdentifierName("Hive.Server.Common.Application"))
                .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            usings.Add(UsingDirective(IdentifierName("Hive.Both.General.Dispatchers"))
                .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            usings.Add(UsingDirective(IdentifierName("Microsoft.Extensions.Logging"))
                .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            var nameSpaceDecl = NamespaceDeclaration(IdentifierName("GeneratedNamespace"));
            var newClass = ClassDeclaration(newClassName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SimpleBaseType(ParseTypeName(messageHandlerType.Name)));


            // Generate code like this:
            /*
                     *  public class ChannelHandlerBinder : IChannelHandlerBinder
                        {
                            public Task BindAndStart(ServerApplicationBase appBase, IDispatcher dispatcher, ILoggerFactory loggerFactory, CancellationToken stoppingToken)
                            {
                                IChannelHandlerBinder binder = this;
                                var app = appBase as ServerApplicationBase;
                                var taskList = new List<Task>
                                {
                                    binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
                                    binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
                                    binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
                                    binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
                                };

                                return Task.WhenAll(taskList);
                            }
                        }
                     *
                     */

            var listInitializeExpressions = new SeparatedSyntaxList<ExpressionSyntax>();

            const string appBaseLocalVarName = "appBase";
            const string stoppingTokenVarName = "stoppingToken";
            const string dispatcherVarName = "dispatcher";
            foreach (var handlerMethod in handlerMethods)
            {
                var methodName = handlerMethod.Identifier.Text;
                var firstParamTypeSyntax = handlerMethod.ParameterList.Parameters[0].Type;
                if (firstParamTypeSyntax is GenericNameSyntax firstParamGenericNameSyntax)
                {
                    firstParamTypeSyntax = firstParamGenericNameSyntax.TypeArgumentList.Arguments[0];
                }

                var requestType = model.GetTypeInfo(firstParamTypeSyntax).Type;

                usings.Add(UsingDirective(IdentifierName(requestType.ContainingNamespace.ToDisplayString()))
                    .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

                var responseType = model.GetTypeInfo(handlerMethod.ReturnType).Type;
                if (handlerMethod.ReturnType is GenericNameSyntax genericNameSyntax) // ValueTask<?>
                {
                    if (genericNameSyntax.TypeArgumentList
                            .Arguments[0] is GenericNameSyntax genericNameSyntax2) // ValueTask<ResultContext<?>>
                    {
                        responseType = model.GetTypeInfo(genericNameSyntax2.TypeArgumentList.Arguments[0]).Type;
                    }
                    else
                    {
                        responseType = model.GetTypeInfo(genericNameSyntax.TypeArgumentList.Arguments[0]).Type;
                    }
                }


                usings.Add(UsingDirective(IdentifierName(responseType.ContainingNamespace.ToDisplayString()))
                    .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));


                const string startMessageProcessLoopFuncName = "StartMessageProcessLoop";
                var invokingExpression = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(appBaseLocalVarName),
                            GenericName(Identifier(startMessageProcessLoopFuncName))
                                .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SeparatedList<TypeSyntax>(
                                            new SyntaxNodeOrToken[]
                                            {
                                                IdentifierName(requestType.Name),
                                                Token(SyntaxKind.CommaToken),
                                                IdentifierName(responseType.Name)
                                            })))))
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(
                                new SyntaxNodeOrToken[]
                                {
                                    Argument(
                                        IdentifierName(dispatcherVarName)),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("app"),
                                            IdentifierName(methodName))),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(
                                        IdentifierName(stoppingTokenVarName))
                                })));

                listInitializeExpressions = listInitializeExpressions.Add(invokingExpression);
            }


            var bindAndStartMethod = MethodDeclaration(ParseTypeName("Task"), "BindAndStart")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier(appBaseLocalVarName))
                        .WithType(ParseTypeName("ServerApplicationBase")),
                    Parameter(Identifier(dispatcherVarName))
                        .WithType(ParseTypeName("IDispatcher")),
                    Parameter(Identifier(stoppingTokenVarName))
                        .WithType(ParseTypeName("CancellationToken"))
                )
                .WithBody(Block(
                    LocalDeclarationStatement(
                        VariableDeclaration(ParseTypeName(messageHandlerType.Name))
                            .AddVariables(VariableDeclarator(Identifier("binder"))
                                .WithInitializer(EqualsValueClause(ThisExpression())))),
                    LocalDeclarationStatement(
                        VariableDeclaration(ParseTypeName(classSyntax.Identifier.Text))
                            .AddVariables(VariableDeclarator(Identifier("app"))
                                .WithInitializer(EqualsValueClause(
                                    CastExpression(ParseTypeName(classSyntax.Identifier.Text),
                                        IdentifierName(appBaseLocalVarName)))))),
                    LocalDeclarationStatement(
                        VariableDeclaration(
                                ParseTypeName("List<Task>"))
                            .WithVariables(
                                SingletonSeparatedList<VariableDeclaratorSyntax>(
                                    VariableDeclarator(
                                            Identifier("taskList"))
                                        .WithInitializer(
                                            EqualsValueClause(
                                                ObjectCreationExpression(
                                                        GenericName(
                                                                Identifier("List"))
                                                            .WithTypeArgumentList(
                                                                TypeArgumentList(
                                                                    SingletonSeparatedList<TypeSyntax>(
                                                                        IdentifierName("Task")))))
                                                    .WithInitializer(
                                                        InitializerExpression(
                                                            SyntaxKind.CollectionInitializerExpression,
                                                            listInitializeExpressions
                                                        )
                                                    )))))),
                    ReturnStatement(InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("Task"),
                                IdentifierName("WhenAll")))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        IdentifierName("taskList"))))))
                ));

            newClass = newClass.AddMembers(bindAndStartMethod);

            var tree = CompilationUnit()
                .AddUsings(usings.ToArray())
                .AddMembers(nameSpaceDecl.AddMembers(newClass));

            context.AddSource($"{newClassName}.g.cs", tree.NormalizeWhitespace().ToFullString());
        }
    }
}