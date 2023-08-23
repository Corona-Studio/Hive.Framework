using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Hive.DataSynchronizer.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Hive.DataSynchronizer.SourceGenerator;

[Generator]
public class DataSynchronizationGenerator : ISourceGenerator
{
    private const string DataSynchronizationObjectInterface =
        "Hive.DataSynchronizer.Abstraction.Interfaces.IDataSynchronizationObject";

    private const string DataSynchronizationObjectAttribute =
        "Hive.DataSynchronizer.Shared.Attributes.DataSynchronizationObjectAttribute";

    private const string DataSynchronizationPropertyAttribute =
        "Hive.DataSynchronizer.Shared.Attributes.DataSynchronizationPropertyAttribute";

    public void Initialize(GeneratorInitializationContext context)
    {
        /*
#if DEBUG
        if (!Debugger.IsAttached) Debugger.Launch();
#endif
        */
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
                        DataSynchronizationObjectAttribute)));

            foreach (var classSyntax in dataSyncClasses)
            {
                var dataSyncAttribute =
                    classSyntax.AttributeLists.SelectMany(attrList => attrList.Attributes)
                        .First(attr =>
                            model.GetTypeInfo(attr).Type?.ToDisplayString() == DataSynchronizationObjectAttribute);

                // Process fields with [DataSynchronizationProperty] attribute
                var dataSyncProperties = classSyntax.Members.OfType<FieldDeclarationSyntax>()
                    .Where(field => field.AttributeLists.Any(attrList =>
                        attrList.Attributes.Any(attr =>
                            model.GetTypeInfo(attr).Type?.ToDisplayString() ==
                            DataSynchronizationPropertyAttribute)));

                // Generate property and PerformUpdate method
                var classNamespace = classSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()
                    ?.Name.ToString();
                var className = classSyntax.Identifier.ValueText;
                var propertiesCode = GenerateProperties(dataSyncProperties, model);
                var performUpdateMethod = GeneratePerformUpdateMethod(dataSyncProperties, model);

                // Generate the entire new class code
                var newClassCode = $$"""
                                     using System;
                                     using System.Collections.Concurrent;
                                     using Hive.DataSynchronizer.Shared.UpdateInfo;
                                     using Hive.DataSynchronizer.Abstraction.Interfaces;

                                     namespace {{classNamespace}}
                                     {
                                        {{classSyntax.Modifiers}} class {{className}} : {{DataSynchronizationObjectInterface}}
                                        {
                                            private readonly ConcurrentDictionary<string, IUpdateInfo> _updatedFields
                                                = new ConcurrentDictionary<string, IUpdateInfo>();
                                     
                                            public ushort ObjectSyncId => {{dataSyncAttribute.ArgumentList!.Arguments.First()}};
                                     
                                            {{propertiesCode}}
                                     
                                            {{performUpdateMethod}}
                                     
                                            {{GenerateNotifyPropertyChangedMethod()}}
                                        }
                                     }
                                     """;
                var newSourceText = CSharpSyntaxTree.ParseText(SourceText.From(newClassCode, Encoding.UTF8)).GetRoot().NormalizeWhitespace().SyntaxTree.GetText();
                var newClassName = $"{className}_Generated.g.cs";

                context.AddSource(newClassName, newSourceText);
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
                        "Hive.DataSynchronizer.Shared.Attributes.UseCustomUpdateInfoTypeAttribute");
            var hasCustomUpdateInfoAttribute = customUpdateInfoTypeAttribute != null;

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

            var generatedProperty = $$"""
                                      public {{fieldType}} {{propertyName}}
                                      {
                                        get => {{fieldName}};
                                        set
                                        {
                                            NotifyPropertyChanged(
                                                nameof({{propertyName}}),
                                                new {{updateInfoType}}(ObjectSyncId, nameof({{propertyName}}), value));
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
               public void NotifyPropertyChanged(string propertyName, IUpdateInfo updateInfo)
               {
                    _updatedFields.AddOrUpdate(propertyName, updateInfo, (d1, d2) => updateInfo);
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
                        "Hive.DataSynchronizer.Shared.Attributes.UseCustomUpdateInfoTypeAttribute");
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
                                    public void PerformUpdate(IUpdateInfo infoBase)
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
            return "BooleanUpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(char), model)))
            return "CharUpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(double), model)))
            return "DoubleUpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(short), model)))
            return "Int16UpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(int), model)))
            return "Int32UpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(long), model)))
            return "Int64UpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(float), model)))
            return "SingleUpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(string), model)))
            return "StringUpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(ushort), model)))
            return "UInt16UpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(uint), model)))
            return "UInt32UpdateInfo";
        if (comparer.Equals(typeSymbol, TypeSymbolHelper.GetTypeSymbolForType(typeof(ulong), model)))
            return "UInt64UpdateInfo";

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
}