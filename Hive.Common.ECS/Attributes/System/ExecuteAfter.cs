using System;

namespace Hive.Common.ECS.Attributes.System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExecuteAfter : Attribute
{
    public ExecuteAfter(Type systemType)
    {
        SystemType = systemType;
    }

    public Type SystemType { get; }
}