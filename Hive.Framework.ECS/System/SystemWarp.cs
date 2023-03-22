using Hive.Framework.ECS.Attributes.System;

namespace Hive.Framework.ECS.System
{
    public record SystemWarp
    {
        private static readonly HashSet<Type> EmptyTypeSet = new();
        public ISystem System { get; }
        public readonly HashSet<Type> executeBeforeSystems = EmptyTypeSet;
        public readonly HashSet<Type> executeAfterSystems = EmptyTypeSet;
        public readonly HashSet<Type> relatedComponents;
        public Type SystemType { get; }
        public SystemWarp(ISystem system)
        {
            System = system;
            SystemType = system.GetType();
            
            var type = system.GetType();

            if (type.GetCustomAttributes(typeof(ExecuteBefore),true) is ExecuteBefore[] { Length: > 0 } executeBeforeAttributes)
            {
                executeBeforeSystems = new HashSet<Type>();
                foreach (var executeBefore in executeBeforeAttributes)
                {
                    executeBeforeSystems.Add(executeBefore.SystemType);
                }
            }
            
            if (type.GetCustomAttributes(typeof(ExecuteAfter),true) is ExecuteAfter[] { Length: > 0 } executeAfterAttributes)
            {
                executeAfterSystems = new HashSet<Type>();
                foreach (var executeBefore in executeAfterAttributes)
                {
                    executeAfterSystems.Add(executeBefore.SystemType);
                }
            }

            relatedComponents = type.IsGenericType ? new HashSet<Type>(type.GetGenericArguments()) : new HashSet<Type>();
        }
    }
}