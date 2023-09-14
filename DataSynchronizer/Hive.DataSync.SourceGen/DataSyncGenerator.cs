using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Hive.DataSync.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hive.DataSync.SourceGenerator;

[Generator]
public class DataSyncGenerator : ISourceGenerator
{
    private const string SyncObjectInterface =
        "Hive.DataSync.Abstraction.Interfaces.ISyncObject";

    private const string SyncObjectAttribute =
        "Hive.DataSync.Shared.Attributes.SyncObjectAttribute";

    private const string SyncPropertyAttribute =
        "Hive.DataSync.Shared.Attributes.SyncPropertyAttribute";

    private const string CustomSerializerAttribute =
        "Hive.DataSync.Shared.Attributes.CustomSerializerAttribute";

    private const string SyncOptionAttribute =
        "Hive.DataSync.Shared.Attributes.SyncOptionAttribute";

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        //if (!Debugger.IsAttached) Debugger.Launch();
#endif
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Retrieve the syntax trees for the compilation
        var syntaxTrees = context.Compilation.SyntaxTrees;

        foreach (var syntaxTree in syntaxTrees)
        {
            var model = context.Compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            // Find all classes with [DataSynchronizationObject] attribute
            var dataSyncClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(cls => cls.AttributeLists.Any(attrList =>
                    attrList.Attributes.Any(attr =>
                        model.GetTypeInfo(attr).Type?.ToDisplayString() ==
                        SyncObjectAttribute)));

            foreach (var classSyntax in dataSyncClasses)
            {
                var dataSyncAttribute =
                    classSyntax.AttributeLists.SelectMany(attrList => attrList.Attributes)
                        .First(attr =>
                            model.GetTypeInfo(attr).Type?.ToDisplayString() == SyncObjectAttribute);

                // Process fields with [DataSynchronizationProperty] attribute
                var dataSyncProperties = classSyntax.Members.OfType<FieldDeclarationSyntax>()
                    .Where(field => field.AttributeLists.Any(attrList =>
                        attrList.Attributes.Any(attr =>
                            model.GetTypeInfo(attr).Type?.ToDisplayString() ==
                            SyncPropertyAttribute))).ToList();

                // Generate property and PerformUpdate method
                var classNamespace = NamespaceHelper.GetNamespace(classSyntax);

                var className = classSyntax.Identifier.ValueText;
                var propertiesCode = GenerateProperties(dataSyncProperties, model);
                var performUpdateMethod = GeneratePerformUpdateMethod(dataSyncProperties, model);


                // Generate the entire new class code
                var newClassCode = $$"""
                                     using System;
                                     using System.CodeDom.Compiler;
                                     using System.Collections.Concurrent;
                                     using System.Collections.Generic;
                                     using System.Linq;
                                     using Hive.Framework.Shared;
                                     using Hive.DataSync.Shared.ObjectSyncPacket;
                                     using Hive.DataSync.Abstraction.Interfaces;

                                     namespace {{classNamespace}}
                                     {
                                        [GeneratedCode("{{nameof(DataSyncGenerator)}}", "{{typeof(DataSyncGenerator).Assembly.GetName().Version?.ToString() ?? "1.0.0.0"}}")]
                                        {{classSyntax.Modifiers}} class {{className}} : {{SyncObjectInterface}}
                                        {
                                            private readonly ConcurrentDictionary<string, ISyncPacket> _updatedFields
                                                = new ConcurrentDictionary<string, ISyncPacket>();
                                     
                                            public ushort ObjectSyncId => {{dataSyncAttribute.ArgumentList!.Arguments.First()}};
                                     
                                            {{propertiesCode}}
                                     
                                            {{performUpdateMethod}}
                                     
                                            {{GenerateNotifyPropertyChangedMethod()}}
                                            
                                            {{GenerateGetPendingChangedMethod()}}
                                        }
                                     }
                                     """;


                // var unit = CompilationUnit();
                // unit = unit.WithUsings(GenUsing());
                // unit = unit.WithMembers(SingletonList(GenClass(classNamespace, className)));
                // var tmp = unit.NormalizeWhitespace().SyntaxTree.GetText();


                var newSourceText = CSharpSyntaxTree.ParseText(SourceText.From(newClassCode, Encoding.UTF8)).GetRoot()
                    .NormalizeWhitespace().SyntaxTree.GetText();

                // newSourceText. += $"/*{tmp}*/";
                var newClassName = $"{className}_Generated.g.cs";

                context.AddSource(newClassName, newSourceText.ToString());
            }
        }
    }

    private string GenerateProperties(IEnumerable<FieldDeclarationSyntax> fields, SemanticModel model)
    {
        var propertiesCodeSb = new StringBuilder();

        foreach (var field in fields)
        {
            var customUpdateInfoTypeAttribute =
                field.AttributeLists
                    .SelectMany(attrList => attrList.Attributes)
                    .FirstOrDefault(attr =>
                        model.GetTypeInfo(attr).Type?.ToDisplayString() ==
                        CustomSerializerAttribute);
            var hasCustomUpdateInfoAttribute = customUpdateInfoTypeAttribute != null;

            var syncOptionAttribute =
                field.AttributeLists
                    .SelectMany(attrList => attrList.Attributes)
                    .FirstOrDefault(attr =>
                        model.GetTypeInfo(attr).Type?.ToDisplayString() ==
                        SyncOptionAttribute);
            var hasSyncOptionAttribute = syncOptionAttribute != null;

            var fieldName = field.Declaration.Variables.First().Identifier.ValueText;
            var fieldType = model.GetTypeInfo(field.Declaration.Type).Type;
            var propertyName = GetGeneratedPropertyName(fieldName);

            string updateInfoType;
            if (hasCustomUpdateInfoAttribute)
            {
                var typeArgument = (TypeOfExpressionSyntax)customUpdateInfoTypeAttribute
                    .ArgumentList!.Arguments.First().Expression;
                var getTypeCode = model.GetTypeInfo(typeArgument.Type).Type;

                updateInfoType = getTypeCode!.ToDisplayString();
            }
            else
            {
                updateInfoType = GetTypeCastCodeBasedOnPropertyType(fieldType, model);
            }

            string syncOptions;
            if (hasSyncOptionAttribute)
            {
                var optionArgument = (MemberAccessExpressionSyntax)syncOptionAttribute
                    .ArgumentList!.Arguments.First().Expression;
                var syncOption = optionArgument.ToString();

                syncOptions = syncOption;
            }
            else
            {
                syncOptions = "SyncOptions.ClientOnly";
            }

            var generatedProperty = $$"""
                                      public {{fieldType}} {{propertyName}}
                                      {
                                        get => {{fieldName}};
                                        set
                                        {
                                            NotifyPropertyChanged(
                                                nameof({{propertyName}}),
                                                new {{updateInfoType}}(ObjectSyncId, nameof({{propertyName}}), {{syncOptions}}, value));
                                            {{fieldName}} = value;
                                        }
                                      }
                                      """;

            propertiesCodeSb.AppendLine(generatedProperty);
        }

        return propertiesCodeSb.ToString();
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

    private string GeneratePerformUpdateMethod(IEnumerable<FieldDeclarationSyntax> fields, SemanticModel model)
    {
        var updateCodeSb = new StringBuilder();

        foreach (var field in fields)
        {
            var customUpdateInfoTypeAttribute =
                field.AttributeLists
                    .SelectMany(attrList => attrList.Attributes)
                    .FirstOrDefault(attr =>
                        model.GetTypeInfo(attr).Type?.ToDisplayString() ==
                        CustomSerializerAttribute);
            var hasCustomUpdateInfoAttribute = customUpdateInfoTypeAttribute != null;

            var fieldName = field.Declaration.Variables.First().Identifier.ValueText;
            var fieldType = model.GetTypeInfo(field.Declaration.Type).Type;

            string updateInfoType;
            if (hasCustomUpdateInfoAttribute)
            {
                var typeArgument = (TypeOfExpressionSyntax)customUpdateInfoTypeAttribute
                    .ArgumentList!.Arguments.First().Expression;
                var getTypeCode = model.GetTypeInfo(typeArgument.Type).Type;

                updateInfoType = getTypeCode!.ToDisplayString();
            }
            else
            {
                updateInfoType = GetTypeCastCodeBasedOnPropertyType(fieldType, model);
            }

            var propertyName = GetGeneratedPropertyName(fieldName);
            var castVariableName = $"resultInfoFor{propertyName}";

            var updateCode = $$"""
                               if (infoBase.PropertyName == nameof({{propertyName}}) && infoBase is {{updateInfoType}} {{castVariableName}})
                               {
                                    {{fieldName}} = {{castVariableName}}.NewValue;
                               }
                               """;

            updateCodeSb.AppendLine(updateCode);
        }

        var performUpdateMethod = $$"""
                                    public void PerformUpdate(ISyncPacket infoBase)
                                    {
                                        if (infoBase == null)
                                            throw new ArgumentNullException(nameof(infoBase));
                                    
                                        {{updateCodeSb}}
                                    }
                                    """;

        return performUpdateMethod;
    }

    private string GetTypeCastCodeBasedOnPropertyType(ITypeSymbol typeSymbol, SemanticModel model)
    {
        var comparer = SymbolEqualityComparer.Default;

        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(bool), model)))
            return "BooleanSyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(char), model)))
            return "CharSyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(double), model)))
            return "DoubleSyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(short), model)))
            return "Int16SyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(int), model)))
            return "Int32SyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(long), model)))
            return "Int64SyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(float), model)))
            return "SingleSyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(string), model)))
            return "StringSyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(ushort), model)))
            return "UInt16SyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(uint), model)))
            return "UInt32SyncPacket";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(ulong), model)))
            return "UInt64SyncPacket";

        return "__unknown__";
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
                            IdentifierName("Abstraction")),
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
                        SingletonSeparatedList<AttributeSyntax>(
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
                TokenList(
                    new[]
                    {
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.PartialKeyword)
                    }));
        return namespaceDeclaration.AddMembers(classDecl);
    }
}