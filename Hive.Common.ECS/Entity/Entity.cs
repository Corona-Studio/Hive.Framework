using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hive.Common.ECS.Entity;

public class Entity : IEntity
{
    //private readonly MultiDictionary<Type, IEntityComponent> _componentMap = new();

    private static readonly ReadOnlyCollection<IEntity> EmptyChildrenCollection = new(Array.Empty<IEntity>());

    private List<IEntity>? _children;
    private ReadOnlyCollection<IEntity>? _readOnlyChildrenCollection;
    protected IEntity parent;

    internal Entity()
    {
    }

    public IECSArch ECSArch { get; protected set; }
    public string Name { get; set; } = string.Empty;
    public int Depth { get; private set; }

    public ReadOnlyCollection<IEntity> Children
    {
        get
        {
            if (_children == null)
                return EmptyChildrenCollection;

            return _readOnlyChildrenCollection ??= new ReadOnlyCollection<IEntity>(_children);
        }
    }

    public virtual IEntity? Parent
    {
        get => parent;
        set
        {
            if (parent == value) return;

            if (parent is Entity entityParent)
                entityParent.RemoveChildren(this);

            parent = value;

            if (parent is Entity newEntityParent) newEntityParent.AddChildren(this);

            Depth = parent == null ? 0 : parent.Depth + 1;
            if (parent != null)
                ECSArch = parent.ECSArch;
        }
    }

    public long InstanceId { get; init; }

    protected virtual void AddChildren(IEntity entity)
    {
        _children ??= new List<IEntity>();

        _children.Add(entity);
    }

    protected virtual void RemoveChildren(IEntity entity)
    {
        _children?.Remove(entity);
    }
}