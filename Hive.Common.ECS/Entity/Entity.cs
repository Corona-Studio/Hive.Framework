using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hive.Common.ECS.Entity
{
    public class Entity : IEntity
    {
        internal Entity()
        {
        }

        private List<IEntity>? _children = null;
        private ReadOnlyCollection<IEntity>? _readOnlyChildrenCollection;
        //private readonly MultiDictionary<Type, IEntityComponent> _componentMap = new();

        private static readonly ReadOnlyCollection<IEntity> EmptyChildrenCollection = new(Array.Empty<IEntity>());
        protected IEntity parent;

        public IECSArch ECSArch { get; protected set; }
        public string Name { get; set; } = string.Empty;
        public int Depth { get; private set; } = 0;

        public ReadOnlyCollection<IEntity> Children
        {
            get
            {
                if (_children == null)
                    return EmptyChildrenCollection;

                return _readOnlyChildrenCollection??=new ReadOnlyCollection<IEntity>(_children);
            }
        }

        protected virtual void AddChildren(IEntity entity)
        {
            _children ??= new List<IEntity>();

            _children.Add(entity);
        }

        protected virtual void RemoveChildren(IEntity entity)
        {
            _children?.Remove(entity);
        }

        public virtual IEntity? Parent
        {
            get => parent;
            set
            {
                if (parent == value) return;
                
                if(parent is Entity entityParent)
                    entityParent.RemoveChildren(this);
                    
                parent = value;

                if (parent is Entity newEntityParent)
                {
                    newEntityParent.AddChildren(this);
                }

                Depth = parent == null ? 0 : parent.Depth + 1;
                if (parent != null)
                    ECSArch = parent.ECSArch;
            }
        }

        public long InstanceId { get; init; }
    }
}