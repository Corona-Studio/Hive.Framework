using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Hive.DataSync.SourceGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hive.DataSync.SourceGen;

// Namespace helper
internal static class NamespaceHelper
{
    public static string GetNamespace(SyntaxNode syntaxNode)
    {
        var namespaceDeclaration = syntaxNode.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        return namespaceDeclaration?.Name.ToString() ?? string.Empty;
    }
}

[Generator]
public class DataSyncGenerator : IIncrementalGenerator
{
    private const string SyncObjectInterface =
        "Hive.DataSync.Abstractions.Interfaces.ISyncObject";

    private const string SyncObjectAttribute =
        "Hive.DataSync.Shared.Attributes.SyncObjectAttribute";

    private const string SyncPropertyAttribute =
        "Hive.DataSync.Shared.Attributes.SyncPropertyAttribute";

    private const string CustomSerializerAttribute =
        "Hive.DataSync.Shared.Attributes.CustomSerializerAttribute";

    private const string SyncOptionAttribute =
        "Hive.DataSync.Shared.Attributes.SyncOptionAttribute";
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Gather all class declarations with the SyncObjectAttribute
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (context, _) => GetDataSyncClassInfo(context))
            .Where(classInfo => classInfo != null)
            .Select((classInfo, _) => classInfo!);

        // Step 2: Combine all class declarations into a single list for processing
        var compilationAndClasses = context.CompilationProvider
            .Combine(classDeclarations.Collect());

        // Step 3: Generate source code for each collected class
        context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
        {
            var (compilation, classes) = source;

            foreach (var classInfo in classes)
            {
                var generatedCode = GenerateClassCode(classInfo!.Value, compilation);
                spc.AddSource($"{classInfo!.Value.Item2}_Generated.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
            }
        });
    }
    
    private static bool HasAttribute(SyntaxNode node, SemanticModel model, string attributeName)
    {
        return node is MemberDeclarationSyntax member &&
               member.AttributeLists.Any(attrList =>
                   attrList.Attributes.Any(attr =>
                       model.GetTypeInfo(attr).Type?.ToDisplayString() == attributeName));
    }
    
    private (string, string, List<FieldDeclarationSyntax>)? GetDataSyncClassInfo(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        if (!HasAttribute(classSyntax, model, SyncObjectAttribute))
            return null;

        // Extract relevant data for code generation
        var classNamespace = NamespaceHelper.GetNamespace(classSyntax);
        var className = classSyntax.Identifier.Text;
        var syncProperties = classSyntax.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(field => HasAttribute(field, model, SyncPropertyAttribute))
            .ToList();

        return (classNamespace, className, syncProperties);
    }
    
    private static (ClassDeclarationSyntax, List<FieldDeclarationSyntax>)? GetClassWithSyncAttributes(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;

        // Check if class has SyncObjectAttribute
        var hasSyncObjectAttr = classDeclaration.AttributeLists
            .SelectMany(attrList => attrList.Attributes)
            .Any(attr => model.GetTypeInfo(attr).Type?.ToDisplayString() == SyncObjectAttribute);

        if (!hasSyncObjectAttr)
            return null;

        var fieldsWithAttributes = classDeclaration.Members.OfType<FieldDeclarationSyntax>()
            .Where(field => field.AttributeLists
                .Any(attrList => attrList.Attributes
                    .Any(attr => model.GetTypeInfo(attr).Type?.ToDisplayString() == SyncPropertyAttribute)))
            .ToList();

        return (classDeclaration, fieldsWithAttributes);
    }

    private string GenerateClassCode((string Namespace, string ClassName, List<FieldDeclarationSyntax> Props) classInfo, Compilation compilation)
    {
        var propertiesCode = GenerateProperties(classInfo.Props, compilation);
        var performUpdateMethod = GeneratePerformUpdateMethod(classInfo.Props, compilation);

        return $$"""
                     using System;
                     using System.CodeDom.Compiler;
                     using System.Collections.Concurrent;
                     using System.Collections.Generic;
                     using System.Linq;
                     using Hive.Framework.Shared;
                     using Hive.DataSync.Shared.ObjectSyncPacket;
                     using Hive.DataSync.Abstractions.Interfaces;
                 
                     namespace {{classInfo.Namespace}}
                     {
                         [GeneratedCode("DataSyncGenerator", "1.0.0.0")]
                         public partial class {{classInfo.ClassName}} : {{SyncObjectInterface}}
                         {
                             private readonly ConcurrentDictionary<string, ISyncPacket> _updatedFields = new ConcurrentDictionary<string, ISyncPacket>();
                 
                             {{propertiesCode}}
                 
                             {{performUpdateMethod}}
                 
                             {{GenerateNotifyPropertyChangedMethod()}}
                 
                             {{GenerateGetPendingChangedMethod()}}
                         }
                     }
                 """;
    }

    private string GenerateProperties(IEnumerable<FieldDeclarationSyntax> fields, Compilation compilation)
    {
        var sb = new StringBuilder();

        foreach (var field in fields)
        {
            var fieldName = field.Declaration.Variables.First().Identifier.ValueText;
            var fieldType = compilation.GetSemanticModel(field.SyntaxTree).GetTypeInfo(field.Declaration.Type).Type;
            var propertyName = GetGeneratedPropertyName(fieldName);

            sb.AppendLine($$"""
                                public {{fieldType}} {{propertyName}}
                                {
                                    get => {{fieldName}};
                                    set
                                    {
                                        NotifyPropertyChanged(nameof({{propertyName}}), new {{GetTypeCastCodeBasedOnPropertyType(fieldType)}}(ObjectSyncId, nameof({{propertyName}}), SyncOptions.ClientOnly, value));
                                        {{fieldName}} = value;
                                    }
                                }
                            """);
        }

        return sb.ToString();
    }

    private string GenerateNotifyPropertyChangedMethod()
    {
        return """
               public void NotifyPropertyChanged(string propertyName, ISyncPacket updateInfo)
               {
                    _updatedFields.AddOrUpdate(propertyName, updateInfo, (d1, d2) => updateInfo);
               }
               """;
    }

    private string GenerateGetPendingChangedMethod()
    {
        return """
               public IEnumerable<ISyncPacket> GetPendingChanges()
               {
                    if (!_updatedFields.Any()) return Enumerable.Empty<ISyncPacket>();
                    
                    var result = _updatedFields.Values.ToList();
                    
                    _updatedFields.Clear();
                    
                    return result;
               }
               """;
    }

    private string GeneratePerformUpdateMethod(IEnumerable<FieldDeclarationSyntax> fields, Compilation compilation)
    {
        var sb = new StringBuilder();
        foreach (var field in fields)
        {
            var fieldName = field.Declaration.Variables.First().Identifier.ValueText;
            var fieldType = compilation.GetSemanticModel(field.SyntaxTree).GetTypeInfo(field.Declaration.Type).Type;
            var propertyName = GetGeneratedPropertyName(fieldName);

            sb.AppendLine($$"""
                                if (infoBase.PropertyName == nameof({{propertyName}}) && infoBase is {{GetTypeCastCodeBasedOnPropertyType(fieldType)}} resultInfoFor{{propertyName}})
                                {
                                    {{fieldName}} = resultInfoFor{{propertyName}}.NewValue;
                                }
                            """);
        }

        return $$"""
               public void PerformUpdate(ISyncPacket infoBase)
               {
                   if (infoBase == null)
                       throw new ArgumentNullException(nameof(infoBase));
               
                   {{sb}}
               }
               """;
    }

    private static string GetTypeCastCodeBasedOnPropertyType(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToString() switch
        {
            "bool" => "BooleanSyncPacket",
            "char" => "CharSyncPacket",
            "double" => "DoubleSyncPacket",
            "short" => "Int16SyncPacket",
            "int" => "Int32SyncPacket",
            "long" => "Int64SyncPacket",
            "float" => "SingleSyncPacket",
            "string" => "StringSyncPacket",
            "ushort" => "UInt16SyncPacket",
            "uint" => "UInt32SyncPacket",
            "ulong" => "UInt64SyncPacket",
            _ => "__unknown__"
        };
    }

    /// <summary>
    ///     Get the generated property name for an input field.
    /// </summary>
    /// <param name="propertyName">The input instance to process.</param>
    /// <returns>The generated property name for <paramref name="propertyName" />.</returns>
    private static string GetGeneratedPropertyName(string propertyName)
    {
        if (propertyName.StartsWith("m_"))
            propertyName = propertyName.Substring(2);
        else if (propertyName.StartsWith("_")) propertyName = propertyName.TrimStart('_');

        return $"{char.ToUpper(propertyName[0], CultureInfo.InvariantCulture)}{propertyName.Substring(1)}";
    }

    private static SyntaxList<UsingDirectiveSyntax> GenUsing()
    {
        return List(
            new[]
            {
                UsingDirective(
                    IdentifierName("System")),
                UsingDirective(
                    QualifiedName(
                        QualifiedName(
                            IdentifierName("System"),
                            IdentifierName("CodeDom")),
                        IdentifierName("Compiler"))),
                UsingDirective(
                    QualifiedName(
                        QualifiedName(
                            IdentifierName("System"),
                            IdentifierName("Collections")),
                        IdentifierName("Concurrent"))),
                UsingDirective(
                    QualifiedName(
                        QualifiedName(
                            IdentifierName("System"),
                            IdentifierName("Collections")),
                        IdentifierName("Generic"))),
                UsingDirective(
                    QualifiedName(
                        IdentifierName("System"),
                        IdentifierName("Linq"))),
                UsingDirective(
                    QualifiedName(
                        QualifiedName(
                            IdentifierName("Hive"),
                            IdentifierName("Framework")),
                        IdentifierName("Shared"))),
                UsingDirective(
                    QualifiedName(
                        QualifiedName(
                            QualifiedName(
                                IdentifierName("Hive"),
                                IdentifierName("DataSync")),
                            IdentifierName("Shared")),
                        IdentifierName("ObjectSyncPacket"))),
                UsingDirective(
                    QualifiedName(
                        QualifiedName(
                            QualifiedName(
                                IdentifierName("Hive"),
                                IdentifierName("DataSync")),
                            IdentifierName("Abstractions")),
                        IdentifierName("Interfaces")))
            });
    }

    private static MemberDeclarationSyntax GenClass(string @namespace, string className)
    {
        var splitNamespace = @namespace.Split('.');
        var namespaceDeclaration = splitNamespace.Aggregate(
            NamespaceDeclaration(
                IdentifierName(splitNamespace.First())),
            (current, next) => current.WithName(
                QualifiedName(
                    current.Name,
                    IdentifierName(next))));
        var classDecl = ClassDeclaration(className).WithAttributeLists(
                SingletonList(
                    AttributeList(
                        SingletonSeparatedList(
                            Attribute(
                                    IdentifierName("GeneratedCode"))
                                .WithArgumentList(
                                    AttributeArgumentList(
                                        SeparatedList<AttributeArgumentSyntax>(
                                            new SyntaxNodeOrToken[]
                                            {
                                                AttributeArgument(
                                                    LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal("DataSyncGenerator"))),
                                                Token(SyntaxKind.CommaToken),
                                                AttributeArgument(
                                                    LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal("1.0.0.0")))
                                            })))))))
            .WithModifiers(
                TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.PartialKeyword)));
        return namespaceDeclaration.AddMembers(classDecl);
    }
}