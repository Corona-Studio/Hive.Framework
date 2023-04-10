using System;
using System.Collections.Generic;
using Hive.Framework.ECS.Attributes.System;
using Hive.Framework.ECS.Entity;
using Hive.Framework.ECS.System.Phases;

namespace Hive.Framework.ECS.System
{
    public class SystemInstance
    {
        private static readonly HashSet<Type> EmptyTypeSet = new();
        public ISystem System { get; }
        public readonly HashSet<Type> ExecuteBeforeSystems = EmptyTypeSet;
        public readonly HashSet<Type> ExecuteAfterSystems = EmptyTypeSet;
        public readonly HashSet<Type> RelatedComponents;
        public readonly List<IEntity> RelatedEntities = new();

        public Type SystemType { get; }
        public SystemInstance(ISystem system)
        {
            System = system;
            SystemType = system.GetType();
            
            var type = system.GetType();

            if (type.GetCustomAttributes(typeof(ExecuteBefore),true) is ExecuteBefore[] { Length: > 0 } executeBeforeAttributes)
            {
                ExecuteBeforeSystems = new HashSet<Type>();
                foreach (var executeBefore in executeBeforeAttributes)
                {
                    ExecuteBeforeSystems.Add(executeBefore.SystemType);
                }
            }
            
            if (type.GetCustomAttributes(typeof(ExecuteAfter),true) is ExecuteAfter[] { Length: > 0 } executeAfterAttributes)
            {
                ExecuteAfterSystems = new HashSet<Type>();
                foreach (var executeBefore in executeAfterAttributes)
                {
                    ExecuteAfterSystems.Add(executeBefore.SystemType);
                }
            }

            RelatedComponents = type.IsGenericType ? new HashSet<Type>(type.GetGenericArguments()) : new HashSet<Type>();
        }

        public void UpdateEntityQueue(IList<IEntity> allEntities)
        {
            ClearEntities();
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < allEntities.Count; i++)
            {
                var entity = allEntities[i];
                if(System.EntityFilter(entity))
                    AddEntity(entity);
            }
        }
        
        private void AddEntity(IEntity entity) => RelatedEntities.Add(entity);

        private void ClearEntities() => RelatedEntities.Clear();

        public void Execute(SystemPhase phase)
        {
            foreach (var entity in RelatedEntities)
            {
                switch (phase)
                {
                    case SystemPhase.Awake:
                        ((IAwakeSystem)System).OnAwake(entity);
                        break;
                    case SystemPhase.LogicUpdate:
                        ((ILogicUpdateSystem)System).OnLogicUpdate(entity);
                        break;
                    case SystemPhase.FrameUpdate:
                        ((IFrameUpdateSystem)System).OnFrameUpdate(entity);
                        break;
                    case SystemPhase.Destroy:
                        ((IDestroySystem)System).OnDestroy(entity);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
                }
            }
        }
    }
}