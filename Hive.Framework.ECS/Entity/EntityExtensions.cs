﻿#nullable enable
using System.Collections;
using Hive.Framework.ECS.Component;
using Hive.Framework.ECS.Compositor;

namespace Hive.Framework.ECS.Entity
{
    public static class EntityExtensions
    {
        private static readonly Queue<IEntity> AuxQueueForBfs = new(1024);
        
        // ReSharper disable once InconsistentNaming
        public class EntityBFSEnumerable : IEnumerable<IEntity>
        {
            private readonly IEntity entity;

            public EntityBFSEnumerable(IEntity entity)
            {
                this.entity = entity;
            }

            public IEnumerator<IEntity> GetEnumerator()
            {
                if(entity==null)
                    yield break;
            
                AuxQueueForBfs.Clear();
                AuxQueueForBfs.Enqueue(entity);
                var numInThisLayer = 1;
                while (AuxQueueForBfs.Count>0)
                {
                    var numInNextLayer = 0;
                    for (var i = 0; i < numInThisLayer; i++)
                    {
                        var node = AuxQueueForBfs.Dequeue();
                        yield return node;
                    
                        numInNextLayer += node.Children.Count;
                        for (var j = 0; j < node.Children.Count; j++)
                        {
                            AuxQueueForBfs.Enqueue(node.Children[j]);
                        }
                    }

                    numInThisLayer = numInNextLayer;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
        
        // ReSharper disable once InconsistentNaming
        public static EntityBFSEnumerable GetBFSEnumerator(this IEntity entity)
        {
            return new EntityBFSEnumerable(entity);
        }

        public static WorldEntity? GetWorld(this IEntity entity)
        {
            return entity switch
            {
                WorldEntity worldEntity => worldEntity,
                ObjectEntity objectEntity => objectEntity.WorldEntity,
                _=>null
            };
        }
        
        public static RootEntity GetRoot(this IEntity entity)
        {
            return entity switch
            {
                WorldEntity worldEntity => worldEntity.Parent as RootEntity,
                ObjectEntity objectEntity => objectEntity.WorldEntity.Parent as RootEntity,
                RootEntity rootEntity => rootEntity,
                _ => throw new ArgumentOutOfRangeException(nameof(entity), entity, null)
            } ?? throw new InvalidOperationException();
        }
        
        public static void InstantiateChild<TCompositor>(this IEntity parent) where TCompositor : ICompositor
        {
            parent.ECSArch.EntityManager.Instantiate<TCompositor>(parent);
        }
        
        public static void Destroy<TCompositor>(this IEntity entity) where TCompositor : ICompositor
        {
            entity.ECSArch.EntityManager.Destroy(entity);
        }
        
        public static void ModifyComponent<TComponent>(this IEntity entity,RefAction<TComponent> supplier) where TComponent : IEntityComponent
        {
            entity.ECSArch.ComponentManager.ModifyComponent(entity.InstanceID, supplier);
        }
        
        public static TComponent? GetComponent<TComponent>(this IEntity entity) where TComponent : IEntityComponent
        {
            return entity.ECSArch.ComponentManager.GetComponent<TComponent>(entity.InstanceID);
        }
        
        public static void AddComponent<TComponent>(this IEntity entity) where TComponent : IEntityComponent,new()
        {
            entity.ECSArch.ComponentManager.AddComponent<TComponent>(entity.InstanceID);
        }
        
        public static void AddComponent<TComponent>(this IEntity entity,TComponent component) where TComponent : IEntityComponent
        {
            entity.ECSArch.ComponentManager.AddComponent<TComponent>(entity.InstanceID,component);
        }
        
        public static void RemoveComponent<TComponent>(this IEntity entity) where TComponent : IEntityComponent
        {
            entity.ECSArch.ComponentManager.RemoveComponent<TComponent>(entity.InstanceID);
        }
    }
}