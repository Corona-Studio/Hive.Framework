using System.Collections.Generic;
using System.Diagnostics;
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
            foreach (var syntaxTree in syntaxTrees)
            {
                var model = context.Compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // Find all classes with [DataSynchronizationObject] attribute
                var appSyntaxs = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(cls => InheritsFrom(model.GetDeclaredSymbol(cls),serverAppBaseType));
               
                foreach (var classSyntax in appSyntaxs)
                {
                    var handlerMethods = new List<MethodDeclarationSyntax>();
                    foreach (var member in classSyntax.Members)
                    {
                        if (member is MethodDeclarationSyntax memberDeclarationSyntax)
                        {
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
                    }
                    

                    if (handlerMethods.Count == 0)
                    {
                        continue;
                    }

                    string newClassName = $"{classSyntax.Identifier.Text}HandlerBinder";

                    var symbol = model.GetSymbolInfo(classSyntax).Symbol;
                    var usings = new List<UsingDirectiveSyntax>();
                    if (symbol != null)
                    {
                        usings.Add(UsingDirective(IdentifierName(symbol.ContainingNamespace.ToDisplayString()))
                            .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                    }

                    usings.Add(UsingDirective(IdentifierName("Hive.Server.Common.Application"))
                        .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                    
                    usings.Add( UsingDirective(IdentifierName("Hive.Both.General.Dispatchers"))
                        .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

                    usings.Add( UsingDirective(IdentifierName("Microsoft.Extensions.Logging"))
                        .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

                    var nameSpaceDecl = NamespaceDeclaration(IdentifierName("GeneratedNamespace"));
                    var newClass = ClassDeclaration(newClassName)
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddBaseListTypes(SimpleBaseType(ParseTypeName("IChannelHandlerBinder")));

                    
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

                    foreach (var handlerMethod in handlerMethods)
                    {
                        var methodName = handlerMethod.Identifier.Text;
                        var requestType = model.GetTypeInfo(handlerMethod.ParameterList.Parameters[1].Type).Type;
                        
                        usings.Add(UsingDirective(IdentifierName(requestType.ContainingNamespace.ToDisplayString()))
                            .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

                        var responseType = model.GetTypeInfo(handlerMethod.ReturnType).Type;
                        if (handlerMethod.ReturnType is GenericNameSyntax genericNameSyntax)
                        {
                            responseType = model.GetTypeInfo(genericNameSyntax.TypeArgumentList.Arguments[0]).Type;
                        }
                        
                        
                        usings.Add(UsingDirective(IdentifierName(responseType.ContainingNamespace.ToDisplayString()))
                            .WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                        
                        

                        var invokingExpression = InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("binder"),
                                    GenericName(Identifier("StartMessageProcessLoop"))
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
                                                IdentifierName("dispatcher")),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(
                                                IdentifierName("loggerFactory")),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("app"),
                                                    IdentifierName(methodName))),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(
                                                IdentifierName("stoppingToken"))
                                        })));

                        listInitializeExpressions = listInitializeExpressions.Add(invokingExpression);
                    }


                    var bindAndStartMethod = MethodDeclaration(ParseTypeName("Task"), "BindAndStart")
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddParameterListParameters(
                            Parameter(Identifier("appBase"))
                                .WithType(ParseTypeName("ServerApplicationBase")),
                            Parameter(Identifier("dispatcher"))
                                .WithType(ParseTypeName("IDispatcher")),
                            Parameter(Identifier("loggerFactory"))
                                .WithType(ParseTypeName("ILoggerFactory")),
                            Parameter(Identifier("stoppingToken"))
                                .WithType(ParseTypeName("CancellationToken"))
                        )
                        .WithBody(Block(
                            LocalDeclarationStatement(
                                VariableDeclaration(ParseTypeName("IChannelHandlerBinder"))
                                    .AddVariables(VariableDeclarator(Identifier("binder"))
                                        .WithInitializer(EqualsValueClause(ThisExpression())))),
                            LocalDeclarationStatement(
                                VariableDeclaration(ParseTypeName(classSyntax.Identifier.Text))
                                    .AddVariables(VariableDeclarator(Identifier("app"))
                                        .WithInitializer(EqualsValueClause(
                                            CastExpression(ParseTypeName(classSyntax.Identifier.Text),
                                                IdentifierName("appBase")))))),
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
    }
}