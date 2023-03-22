namespace Hive.Framework.ECS.Component
{
    public class ComponentManager : IBelongToECSArch
    {
        private readonly Dictionary<Type, object> typeToComponentListDict = new();

        public ComponentManager(IECSArch arch)
        {
            Arch = arch;
        }

        public void AddComponent<TComponent>(int entityInstanceID) where TComponent : IEntityComponent, new()
        {
            AddComponent(entityInstanceID, new TComponent());
        }

        public void AddComponent<TComponent>(int entityInstanceID, TComponent component)
            where TComponent : IEntityComponent
        {
            Dictionary<int, TComponent> entityComponents;
            if (typeToComponentListDict.TryGetValue(typeof(TComponent), out var list))
            {
                entityComponents = list as Dictionary<int, TComponent>;
            }
            else
            {
                entityComponents = new Dictionary<int, TComponent>();
                typeToComponentListDict.Add(typeof(TComponent), entityComponents);
            }


            if (entityComponents!.ContainsKey(entityInstanceID))
                return;

            entityComponents.Add(entityInstanceID, component);
        }

        public void RemoveComponent<TComponent>(int entityInstanceID) where TComponent : IEntityComponent
        {
            if (typeToComponentListDict.TryGetValue(typeof(TComponent), out var list))
            {
                var entityComponents = (list as Dictionary<int, TComponent>);
                entityComponents?.Remove(entityInstanceID);
            }
        }

        public void ModifyComponent<TComponent>(int entityInstanceID, RefAction<TComponent> supplier)
            where TComponent : IEntityComponent
        {
            if (typeToComponentListDict.TryGetValue(typeof(TComponent), out var list))
            {
                if (list is not Dictionary<int, TComponent> entityComponents) return;

                if (entityComponents.TryGetValue(entityInstanceID, out var component))
                {
                    supplier(ref component);
                    entityComponents[entityInstanceID] = component;
                }
            }
        }

        public TComponent GetComponent<TComponent>(int entityInstanceID) where TComponent : IEntityComponent
        {
            if (typeToComponentListDict.TryGetValue(typeof(TComponent), out var list))
            {
                if (list is not Dictionary<int, TComponent> entityComponents) return default;

                if (entityComponents.TryGetValue(entityInstanceID, out var component))
                {
                    return component;
                }
            }

            return default;
        }

        public void SetComponent<TComponent>(int entityInstanceID, TComponent component)
            where TComponent : IEntityComponent
        {
            if (typeToComponentListDict.TryGetValue(typeof(TComponent), out var list))
            {
                if (list is not Dictionary<int, TComponent> entityComponents) return;

                if (entityComponents.ContainsKey(entityInstanceID))
                {
                    entityComponents[entityInstanceID] = component;
                }
            }
        }

        public IECSArch Arch { get; }
    }
}