using System;

namespace Hive.Framework.ECS.Attributes.System
{
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ExecuteAfter : Attribute
    {
        public Type SystemType { get; }

        public ExecuteAfter(Type systemType)
        {
            SystemType = systemType;
        }
    }
}