using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Hive.Common.ECS.Component
{
    public class ComponentManager : IBelongToECSArch
    {
        private readonly Dictionary<Type, object> _typeToComponentListDict = new();

        public ComponentManager(IECSArch arch)
        {
            Arch = arch;
        }

        private ComponentList<TComp> GetComponentList<TComp>() where TComp : IEntityComponent
        {
            ComponentList<TComp> compList;
            if (_typeToComponentListDict.TryGetValue(typeof(TComp), out var list))
            {
                compList = (ComponentList<TComp>)list;
            }else{
                compList = new ComponentList<TComp>();
                _typeToComponentListDict.Add(typeof(TComp), compList);
            }

            return compList;
        }

        public void AddComponent<TComp>(long entityId) where TComp : IEntityComponent, new()
        {
            AddComponent(entityId, new TComp());
        }

        public void AddComponent<TComp>(long entityId, TComp component)
            where TComp : IEntityComponent
        {
            var componentList = GetComponentList<TComp>();

            componentList.AttachToEntity(entityId, component);
        }

        public void RemoveComponent<TComp>(long entityId) where TComp : IEntityComponent
        {
            var componentList = GetComponentList<TComp>();
            componentList.DetachFromEntity(entityId);
        }

        public void UpdateComponent<TComp>(long entityId, RefAction<TComp> supplier)
            where TComp : IEntityComponent
        {
            var componentList = GetComponentList<TComp>();
            componentList.Update(entityId,supplier);
        }

        public TComp? GetComponent<TComp>(long entityId) where TComp : IEntityComponent
        {
            var componentList = GetComponentList<TComp>();
            ref var comp = ref componentList.GetRefByEntityId(entityId);
            return Unsafe.IsNullRef(ref comp) ? default : comp;
        }

        public void SetComponent<TComp>(long entityId, TComp comp)
            where TComp : IEntityComponent
        {
            var componentList = GetComponentList<TComp>();
            ref var component = ref componentList.GetRefByEntityId(entityId);
            
            if (Unsafe.IsNullRef(ref component))
                return;
            
            component = comp;
        }

        public IECSArch Arch { get; }
    }
}