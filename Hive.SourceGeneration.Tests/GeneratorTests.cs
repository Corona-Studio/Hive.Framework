using Corona.SourceGeneration.Testing;
using Hive.DataSync.SourceGen;
using Hive.Server.Common.Application.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Hive.SourceGeneration.Tests;

[TestFixture]
public sealed class GeneratorTests
{
    private const string SyncSupport = """
        namespace Hive.Common.Shared { public enum SyncOptions { ClientOnly, ServerOnly } }
        namespace Hive.DataSync.Shared.Attributes
        {
            public sealed class SyncObjectAttribute(ushort id) : System.Attribute;
            public sealed class SyncPropertyAttribute : System.Attribute;
            public sealed class SyncOptionAttribute(Hive.Common.Shared.SyncOptions option) : System.Attribute;
            public sealed class CustomSerializerAttribute(System.Type type) : System.Attribute;
        }
        namespace Hive.DataSync.Abstractions.Interfaces
        {
            public interface ISyncPacket { string PropertyName { get; } }
            public interface ISyncObject
            {
                ushort ObjectSyncId { get; }
                void PerformUpdate(ISyncPacket packet);
                void NotifyPropertyChanged(string name, ISyncPacket packet);
                System.Collections.Generic.IEnumerable<ISyncPacket> GetPendingChanges();
            }
        }
        namespace Hive.DataSync.Shared.ObjectSyncPacket
        {
            public sealed class Int32SyncPacket(ushort id, string property, Hive.Common.Shared.SyncOptions option, int value) : Hive.DataSync.Abstractions.Interfaces.ISyncPacket
            { public string PropertyName => property; public int NewValue => value; }
        }
        """;

    [Test]
    public void SyncFieldsFromAllPartsNestedTypesOptionsAndCustomSerializersCompile()
    {
        var source = """
            namespace Example;
            public sealed class GuidPacket(ushort id, string property, Hive.Common.Shared.SyncOptions option, System.Guid value) : Hive.DataSync.Abstractions.Interfaces.ISyncPacket
            { public string PropertyName => property; public System.Guid NewValue => value; }
            public partial class Outer<T> where T : class
            {
                [Hive.DataSync.Shared.Attributes.SyncObject(7)] public partial class Inner
                {
                    [Hive.DataSync.Shared.Attributes.SyncProperty, Hive.DataSync.Shared.Attributes.SyncOption(Hive.Common.Shared.SyncOptions.ServerOnly)] private int _value;
                }
                public partial class Inner
                {
                    [Hive.DataSync.Shared.Attributes.SyncProperty, Hive.DataSync.Shared.Attributes.CustomSerializer(typeof(GuidPacket))] private System.Guid _id;
                }
            }
            """;
        var compilation = GeneratorTestHost.Compilation(SyncSupport).AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(source, GeneratorTestHost.ParseOptions));
        var driver = GeneratorTestHost.Run(GeneratorTestHost.Driver(new DataSyncGenerator()), compilation);
        Assert.That(GeneratorTestHost.Text(driver), Does.Contain("ObjectSyncId => 7"));
        Assert.That(GeneratorTestHost.Text(driver), Does.Contain("global::Example.GuidPacket"));
        Assert.That(GeneratorTestHost.Text(driver), Does.Contain("(global::Hive.Common.Shared.SyncOptions)1"));
        driver = GeneratorTestHost.Run(driver, compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("class Unrelated;", GeneratorTestHost.ParseOptions)));
        Assert.That(driver.GetRunResult().Results.Single().TrackedSteps["SyncModels"].SelectMany(static step => step.Outputs)
            .All(static output => output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged), Is.True);
    }

    [Test]
    public void UnsupportedAndReadonlySyncFieldsProduceDiagnostics()
    {
        foreach (var field in new[] { "private System.Guid _id;", "private readonly int _value;", "private int ___;" })
        {
            var source = "[Hive.DataSync.Shared.Attributes.SyncObject(1)] public partial class Owner { [Hive.DataSync.Shared.Attributes.SyncProperty] " + field + " }";
            var driver = GeneratorTestHost.Driver(new DataSyncGenerator()).RunGenerators(GeneratorTestHost.Compilation(SyncSupport + source));
            Assert.That(driver.GetRunResult().Diagnostics.Select(static d => d.Id), Contains.Item("HIVESYNC001"));
            Assert.That(driver.GetRunResult().Diagnostics.Select(static d => d.Id), Does.Not.Contain("CS8785"));
        }
    }

    [Test]
    public void SyncGeneratedCodeCanUpdateAndDrainChanges()
    {
        var compilation = GeneratorTestHost.Compilation(SyncSupport + """
            [Hive.DataSync.Shared.Attributes.SyncObject(9)] public partial class Owner
            { [Hive.DataSync.Shared.Attributes.SyncProperty] private int _value; }
            """);
        var driver = GeneratorTestHost.Driver(new DataSyncGenerator()).RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        using var stream = new MemoryStream();
        var emit = output.Emit(stream);
        Assert.That(emit.Success, Is.True, string.Join("\n", emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var type = assembly.GetType("Owner")!;
        var owner = Activator.CreateInstance(type)!;
        type.GetProperty("Value")!.SetValue(owner, 42);
        Assert.That(type.GetProperty("Value")!.GetValue(owner), Is.EqualTo(42));
        Assert.That(((System.Collections.IEnumerable)type.GetMethod("GetPendingChanges")!.Invoke(owner, null)!).Cast<object>().Count(), Is.EqualTo(1));
        Assert.That(((System.Collections.IEnumerable)type.GetMethod("GetPendingChanges")!.Invoke(owner, null)!).Cast<object>(), Is.Empty);
    }

    private const string BinderSupport = """
        namespace Hive.Network.Abstractions.Session { public interface ISession; }
        namespace Hive.Both.General.Dispatchers
        {
            public interface IDispatcher;
            public sealed class MessageContext<T>;
            public sealed class ResultContext<T>;
        }
        namespace Hive.Server.Common.Application
        {
            public sealed class MessageHandlerAttribute : System.Attribute;
            public interface IMessageHandlerBinder
            {
                System.Threading.Tasks.Task BindAndStart(ServerApplicationBase app, Hive.Both.General.Dispatchers.IDispatcher dispatcher, System.Threading.CancellationToken token);
            }
            public abstract class ServerApplicationBase
            {
                public System.Threading.Tasks.Task StartMessageProcessLoop<T, R>(Hive.Both.General.Dispatchers.IDispatcher dispatcher, System.Func<T, System.Threading.Tasks.ValueTask<Hive.Both.General.Dispatchers.ResultContext<R>>> handler, System.Threading.CancellationToken token) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task StartMessageProcessLoop<T, R>(Hive.Both.General.Dispatchers.IDispatcher dispatcher, System.Func<Hive.Both.General.Dispatchers.MessageContext<T>, System.Threading.Tasks.ValueTask<Hive.Both.General.Dispatchers.ResultContext<R>>> handler, System.Threading.CancellationToken token) => System.Threading.Tasks.Task.CompletedTask;
            }
        }
        """;

    [Test]
    public void ApplicationMethodsAreDiscoveredWithoutClassAttributesAndUnwrapContexts()
    {
        var compilation = GeneratorTestHost.Compilation(BinderSupport + """
            public partial class App : Hive.Server.Common.Application.ServerApplicationBase
            {
                [Hive.Server.Common.Application.MessageHandler]
                public System.Threading.Tasks.ValueTask<Hive.Both.General.Dispatchers.ResultContext<int>> Raw(string value) => default;
            }
            public partial class App
            {
                [Hive.Server.Common.Application.MessageHandler]
                public System.Threading.Tasks.ValueTask<Hive.Both.General.Dispatchers.ResultContext<string>> Context(Hive.Both.General.Dispatchers.MessageContext<int> context) => default;
            }
            """);
        var driver = GeneratorTestHost.Run(GeneratorTestHost.Driver(new AppMessageHandlerBinderGen()), compilation);
        Assert.That(driver.GetRunResult().GeneratedTrees, Has.Length.EqualTo(1));
        Assert.That(GeneratorTestHost.Text(driver), Does.Contain("StartMessageProcessLoop<int, string>"));
        driver = GeneratorTestHost.Run(driver, compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("class Unrelated;", GeneratorTestHost.ParseOptions)));
        Assert.That(driver.GetRunResult().Results.Single().TrackedSteps["BinderModels"].SelectMany(static step => step.Outputs)
            .All(static output => output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged), Is.True);
    }

    [Test]
    public void InvalidApplicationHandlerReportsDiagnostic()
    {
        var driver = GeneratorTestHost.Driver(new AppMessageHandlerBinderGen()).RunGenerators(GeneratorTestHost.Compilation(BinderSupport + """
            public class App : Hive.Server.Common.Application.ServerApplicationBase
            { [Hive.Server.Common.Application.MessageHandler] private void Wrong() { } }
            """));
        Assert.That(driver.GetRunResult().Diagnostics.Select(static d => d.Id), Contains.Item("HIVEAPP001"));
    }
}
