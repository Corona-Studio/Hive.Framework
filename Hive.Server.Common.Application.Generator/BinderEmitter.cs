using Corona.SourceGeneration;

namespace Hive.Server.Common.Application.SourceGen;

internal static class BinderEmitter
{
    public static SourceFile Emit(BinderModel model)
    {
        var owner = model.Methods[0];
        var writer = new CodeWriter().Header();
        if (owner.Namespace.Length > 0) writer.Open("namespace " + owner.Namespace);
        writer.Open($"public class {owner.BinderName} : global::Hive.Server.Common.Application.IMessageHandlerBinder")
            .Open("public global::System.Threading.Tasks.Task BindAndStart(global::Hive.Server.Common.Application.ServerApplicationBase appBase, global::Hive.Both.General.Dispatchers.IDispatcher dispatcher, global::System.Threading.CancellationToken stoppingToken)")
            .Line($"var app = ({owner.Owner})appBase;")
            .Line("return global::System.Threading.Tasks.Task.WhenAll(new global::System.Threading.Tasks.Task[]")
            .Line("{");
        foreach (var method in model.Methods)
            writer.Line($"    appBase.StartMessageProcessLoop<{method.Request}, {method.Reply}>(dispatcher, app.{method.Name}, stoppingToken),");
        writer.Line("});").Close().Close();
        if (owner.Namespace.Length > 0) writer.Close();
        return new(CSharpNames.HintName(model.Identity) + ".HandlerBinder.g.cs", writer.ToString());
    }
}
