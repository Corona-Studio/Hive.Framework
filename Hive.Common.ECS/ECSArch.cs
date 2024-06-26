﻿using System.Linq;
using Hive.Common.ECS.Component;
using Hive.Common.ECS.Compositor;
using Hive.Common.ECS.Entity;
using Hive.Common.ECS.System;

namespace Hive.Common.ECS;

// ReSharper disable once InconsistentNaming
public class ECSCore : IECSArch
{
    public ECSCore()
    {
        SystemManager = new SystemManager(this);
        EntityManager = new EntityManager(this);
        ComponentManager = new ComponentManager(this);
    }

    public SystemManager SystemManager { get; }
    public EntityManager EntityManager { get; }
    public ComponentManager ComponentManager { get; }

    public void Instantiate<TCompositor>(IEntity? parent = null) where TCompositor : ICompositor
    {
        EntityManager.Instantiate<TCompositor>(parent);
    }

    public void Destroy(IEntity entity)
    {
        EntityManager.Destroy(entity);
    }

    public void AddSystem<T>() where T : ISystem, new()
    {
        var system = new T();
        AddSystem(system);
    }

    public void AddSystem<T>(T system) where T : ISystem
    {
        SystemManager.RegisterSystem(system);
    }

    public void LastUpdate()
    {
        SystemManager.ExecuteSystems(SystemPhase.Awake, EntityManager.GetEntityReadyToAwake().ToList());
        EntityManager.ApplyAwake();
    }

    public void LastPostLateUpdate()
    {
        SystemManager.ExecuteSystems(SystemPhase.Destroy, EntityManager.GetEntityReadyToDestroy().ToList());
        EntityManager.ApplyDestroy();
        EntityManager.UpdateBfsEnumerateList();
    }

    public void FixedUpdate()
    {
        SystemManager.ExecuteSystems(SystemPhase.LogicUpdate, EntityManager.EntityBfsEnumerateList);
    }

    public void Update()
    {
        SystemManager.ExecuteSystems(SystemPhase.FrameUpdate, EntityManager.EntityBfsEnumerateList);
    }
}