namespace Hive.Framework.ECS.Component
{
    public class ComponentManager : IBelongToECSArch
    {
        private readonly Dictionary<Type, object> _typeToComponentListDict = new();

        public ComponentManager(IECSArch arch)
        {
            Arch = arch;
        }

        public void AddComponent<TComp>(int entityId) where TComp : IEntityComponent, new()
        {
            AddComponent(entityId, new TComp());
        }

        public void AddComponent<TComp>(int entityId, TComp component)
            where TComp : IEntityComponent
        {
            ComponentList<TComp> componentList;
            if (_typeToComponentListDict.TryGetValue(typeof(TComp), out var list))
            {
                componentList = (ComponentList<TComp>)list;
            }else{
                componentList = new ComponentList<TComp>();
                _typeToComponentListDict.Add(typeof(TComp), componentList);
            }

            componentList.AttachToEntity(entityId, component);
        }

        public void RemoveComponent<TComp>(int entityId) where TComp : IEntityComponent
        {
            if (_typeToComponentListDict.TryGetValue(typeof(TComp), out var list))
            {
                var entityComponents = (ComponentList<TComp>)list;
                entityComponents.DetachFromEntity(entityId);
            }
        }

        public void ModifyComponent<TComp>(int entityId, RefAction<TComp> supplier)
            where TComp : IEntityComponent
        {
            if (_typeToComponentListDict.TryGetValue(typeof(TComp), out var list))
            {
                var componentList = (ComponentList<TComp>)list;
                componentList.Modify(entityId,supplier);
            }
        }

        public TComp GetComponent<TComp>(int entityId) where TComp : IEntityComponent
        {
            if (_typeToComponentListDict.TryGetValue(typeof(TComp), out var list))
            {
                if (list is not Dictionary<int, TComp> comps) return default;

                if (comps.TryGetValue(entityId, out var component))
                {
                    return component;
                }
            }

            return default;
        }

        public void SeTComp<TComp>(int entityId, TComp comp)
            where TComp : IEntityComponent
        {
            if (_typeToComponentListDict.TryGetValue(typeof(TComp), out var list))
            {
                if (list is not Dictionary<int, TComp> entityComponents) return;

                if (entityComponents.ContainsKey(entityId))
                {
                    entityComponents[entityId] = comp;
                }
            }
        }

        public IECSArch Arch { get; }
    }
}