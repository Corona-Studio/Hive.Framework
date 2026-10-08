using Corona.SourceGeneration;

namespace Hive.DataSync.SourceGen;

internal static class SyncEmitter
{
    private const string Packet = "global::Hive.DataSync.Abstractions.Interfaces.ISyncPacket";
    public static SourceFile Emit(SyncModel model)
    {
        var writer = new CodeWriter().Header().Line("using System.Linq;");
        model.Type.Open(writer, " : global::Hive.DataSync.Abstractions.Interfaces.ISyncObject");
        if (model.GenerateId) writer.Line($"public ushort ObjectSyncId => {model.Id};");
        writer.Line($"private readonly global::System.Collections.Concurrent.ConcurrentDictionary<string, {Packet}> _updatedFields = new();");
        foreach (var field in model.Fields)
            writer.Line().Open($"public {field.Type} {field.Property}").Line($"get => {field.Field};")
                .Open("set").Line($"NotifyPropertyChanged(nameof({field.Property}), new {field.Packet}(ObjectSyncId, nameof({field.Property}), {field.Options}, value));")
                .Line($"{field.Field} = value;").Close().Close();
        writer.Line().Open($"public void NotifyPropertyChanged(string propertyName, {Packet} updateInfo)")
            .Line("_updatedFields.AddOrUpdate(propertyName, updateInfo, (_, _) => updateInfo);").Close();
        writer.Line().Open($"public global::System.Collections.Generic.IEnumerable<{Packet}> GetPendingChanges()")
            .Line("var result = _updatedFields.Values.ToList();").Line("_updatedFields.Clear();").Line("return result;").Close();
        writer.Line().Open($"public void PerformUpdate({Packet} infoBase)")
            .Line("if (infoBase is null) throw new global::System.ArgumentNullException(nameof(infoBase));");
        for (var i = 0; i < model.Fields.Count; i++)
        {
            var field = model.Fields[i];
            writer.Open($"if (infoBase.PropertyName == nameof({field.Property}) && infoBase is {field.Packet} update{i})")
                .Line($"{field.Field} = update{i}.NewValue;").Close();
        }
        writer.Close();
        model.Type.Close(writer);
        return new(CSharpNames.HintName(model.Type.Identity) + ".DataSync.g.cs", writer.ToString());
    }
}
