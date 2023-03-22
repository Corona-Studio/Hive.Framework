#nullable enable
using Hive.Framework.ECS.Component;
using Hive.Framework.ECS.Compositor;
using Hive.Framework.ECS.Entity;
using Hive.Framework.ECS.System;

namespace Hive.Framework.ECS
{
    // ReSharper disable once InconsistentNaming

    public class ECSCore : IECSArch
    {
        public SystemManager SystemManager { get; }
        public EntityManager EntityManager { get; }
        public ComponentManager ComponentManager { get; }

        public ECSCore()
        {
            SystemManager = new SystemManager(this);
            EntityManager = new EntityManager(this);
            ComponentManager = new ComponentManager(this);
        }

        public void Instantiate<TCompositor>(IEntity? parent = null) where TCompositor : ICompositor
        {
            EntityManager.Instantiate<TCompositor>(parent);
        }

        public void Destroy(IEntity entity)
        {
            EntityManager.Destroy(entity);
        }

        public void AddSystem<T>() where T : ISystem,new()
        {
            var system = new T();
            AddSystem(system);
        }

        public void AddSystem<T>(T system) where T : ISystem
        {
            SystemManager.RegisterSystem(system);
        }
        public void LastUpdate ()
        {
            SystemManager.ExecuteSystems(SystemPhase.Awake, EntityManager.GetEntityReadyToAwake().ToList());
            EntityManager.ApplyAwake();
        }
        public void LastPostLateUpdate ()
        {
            SystemManager.ExecuteSystems(SystemPhase.Destroy, EntityManager.GetEntityReadyToDestroy().ToList());
            EntityManager.ApplyDestroy();
            EntityManager.UpdateBfsEnumerateList();
        }

        public void FixedUpdate()
        {
            SystemManager.ExecuteSystems(SystemPhase.LogicUpdate,EntityManager.EntityBfsEnumerateList);
        }
        
        public void Update()
        {
            SystemManager.ExecuteSystems(SystemPhase.FrameUpdate,EntityManager.EntityBfsEnumerateList);
        }
    }
}