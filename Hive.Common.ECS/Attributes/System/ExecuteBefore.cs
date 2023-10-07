using System;

namespace Hive.Common.ECS.Attributes.System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExecuteBefore : Attribute
{
    public ExecuteBefore(Type systemType)
    {
        SystemType = systemType;
    }

    public Type SystemType { get; }
}