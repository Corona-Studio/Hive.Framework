using System;
using System.Collections.Generic;
using System.Linq;
using Hive.Common.ECS.Compositor;
using Yitter.IdGenerator;

namespace Hive.Common.ECS.Entity
{
    public class EntityManager : ICanManageEntity,IBelongToECSArch
    {
        public IECSArch Arch { get; }
        public RootEntity Root { get; }
        public WorldEntity CurrentWorld { get; }
        public List<IEntity> EntityBfsEnumerateList { get; } = new();
        private static readonly Dictionary<Type, ICompositor> TypeToCompositorDict = new();

        private readonly SortedSet<IEntity> _entitiesToAwakeSet;
        private readonly SortedSet<IEntity> _entitiesToDestroySet;

        private readonly EntityExtensions.EntityBFSEnumerable _bfsEnumerable;

        private static long AllocateEntityId()
        {
            return YitIdHelper.NextId();
        }
        
        public EntityManager(IECSArch arch)
        {
            Arch = arch;
            var entityDepthComparison = new EntityDepthComparer();
            _entitiesToAwakeSet = new SortedSet<IEntity>(entityDepthComparison);
            _entitiesToDestroySet = new SortedSet<IEntity>(entityDepthComparison);
            
            Root = new RootEntity(arch);
            CurrentWorld = new WorldEntity
            {
                Name = "Default World",
                Parent = Root
            };

            _bfsEnumerable = Root.GetBFSEnumerator();
        }
        private static ICompositor? GetOrCreateCompositor<TCompositor>() where TCompositor : ICompositor
        {
            if (TypeToCompositorDict.TryGetValue(typeof(TCompositor), out var compositor))
                return compositor;

            var instance = Activator.CreateInstance<TCompositor>();
            if (instance == null)
                return null;

            TypeToCompositorDict.Add(typeof(TCompositor), instance);

            return instance;
        }

        public void Instantiate<TCompositor>(IEntity? parent = null) where TCompositor : ICompositor
        {
            var compositor = GetOrCreateCompositor<TCompositor>();
            if (compositor == null)
                throw new InvalidOperationException(
                    $"Fail to get compositor of type: '{typeof(TCompositor).FullName}'");

            parent ??= Root;

            if (_entitiesToDestroySet.Contains(parent))
                throw new InvalidOperationException(
                    $"Parent Entity are being destroying.");

            var entity = compositor.Composite(AllocateEntityId(), parent);
            
            _entitiesToAwakeSet.Add(entity);
        }

        public void Destroy(IEntity entity)
        {
            foreach (var child in entity.GetBFSEnumerator())
            {
                _entitiesToDestroySet.Add(child);
            }
        }
        
        private void RemoveEntityFromEntityTree(IEntity entity)
        {
            entity.Parent = null;
        }

        public IEnumerable<IEntity> GetEntityReadyToDestroy()
        {
            return _entitiesToDestroySet;
        }
        
        public IEnumerable<IEntity> GetEntityReadyToAwake()
        {
            return _entitiesToAwakeSet;
        }

        public void ApplyAwake()
        {
            if (_entitiesToAwakeSet.Count > 0)
            {
                _entitiesToAwakeSet.Clear();
            }
        }
        
        public void ApplyDestroy()
        {
            if (_entitiesToDestroySet.Count > 0)
            {
                var enumerable = _entitiesToDestroySet.Where(entity => !_entitiesToDestroySet.Contains(entity.Parent));
                foreach (var entity in enumerable)
                {
                    RemoveEntityFromEntityTree(entity);
                }
                _entitiesToDestroySet.Clear();
            }
        }

        public void UpdateBfsEnumerateList()
        {
            EntityBfsEnumerateList.Clear();
            EntityBfsEnumerateList.AddRange(_bfsEnumerable);
        }
    }
}