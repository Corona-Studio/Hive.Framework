using System;
using System.Collections;
using System.Collections.Generic;
using Hive.Common.ECS.Component;
using Hive.Common.ECS.Compositor;

namespace Hive.Common.ECS.Entity;

public static class EntityExtensions
{
    private static readonly Queue<IEntity> AuxQueueForBfs = new(1024);

    extension(IEntity entity)
    {
        // ReSharper disable once InconsistentNaming
        public EntityBFSEnumerable GetBFSEnumerator()
        {
            return new EntityBFSEnumerable(entity);
        }

        public WorldEntity? GetWorld()
        {
            return entity switch
            {
                WorldEntity worldEntity => worldEntity,
                ObjectEntity objectEntity => objectEntity.WorldEntity,
                _ => null
            };
        }

        public RootEntity GetRoot()
        {
            return entity switch
            {
                WorldEntity worldEntity => worldEntity.Parent as RootEntity,
                ObjectEntity objectEntity => objectEntity.WorldEntity?.Parent as RootEntity,
                RootEntity rootEntity => rootEntity,
                _ => throw new ArgumentOutOfRangeException(nameof(entity), entity, null)
            } ?? throw new InvalidOperationException();
        }

        public void InstantiateChild<TCompositor>() where TCompositor : ICompositor
        {
            entity.ECSArch.EntityManager.Instantiate<TCompositor>(entity);
        }

        public void Destroy<TCompositor>() where TCompositor : ICompositor
        {
            entity.ECSArch.EntityManager.Destroy(entity);
        }

        public void UpdateComponent<TComponent>(RefAction<TComponent> supplier)
            where TComponent : IEntityComponent
        {
            entity.ECSArch.ComponentManager.UpdateComponent(entity.InstanceId, supplier);
        }

        public TComponent? GetComponent<TComponent>() where TComponent : IEntityComponent
        {
            return entity.ECSArch.ComponentManager.GetComponent<TComponent>(entity.InstanceId);
        }

        public void AddComponent<TComponent>() where TComponent : IEntityComponent, new()
        {
            entity.ECSArch.ComponentManager.AddComponent<TComponent>(entity.InstanceId);
        }

        public void AddComponent<TComponent>(TComponent component)
            where TComponent : IEntityComponent
        {
            entity.ECSArch.ComponentManager.AddComponent(entity.InstanceId, component);
        }

        public void RemoveComponent<TComponent>() where TComponent : IEntityComponent
        {
            entity.ECSArch.ComponentManager.RemoveComponent<TComponent>(entity.InstanceId);
        }
    }

    // ReSharper disable once InconsistentNaming
    public class EntityBFSEnumerable : IEnumerable<IEntity>
    {
        private readonly IEntity? _entity;

        public EntityBFSEnumerable(IEntity? entity)
        {
            _entity = entity;
        }

        public IEnumerator<IEntity> GetEnumerator()
        {
            if (_entity == null)
                yield break;

            AuxQueueForBfs.Clear();
            AuxQueueForBfs.Enqueue(_entity);
            var numInThisLayer = 1;
            while (AuxQueueForBfs.Count > 0)
            {
                var numInNextLayer = 0;
                for (var i = 0; i < numInThisLayer; i++)
                {
                    var node = AuxQueueForBfs.Dequeue();
                    yield return node;

                    numInNextLayer += node.Children.Count;
                    for (var j = 0; j < node.Children.Count; j++) AuxQueueForBfs.Enqueue(node.Children[j]);
                }

                numInThisLayer = numInNextLayer;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}