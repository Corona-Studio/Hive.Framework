using System;

namespace Hive.Common.ECS.Attributes.System
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ExecuteBefore : Attribute
    {
        public Type SystemType { get; }

        public ExecuteBefore(Type systemType)
        {
            SystemType = systemType;
        }
    }
}