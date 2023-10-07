using System;
using System.Collections.Generic;
using Hive.Common.ECS.System.Phases;

namespace Hive.Common.ECS.System;

public struct PhaseInterfaceType
{
    public static readonly PhaseInterfaceType AwakeInterfaceType = typeof(IAwakeSystem);
    public static readonly PhaseInterfaceType LogicUpdateInterfaceType = typeof(ILogicUpdateSystem);
    public static readonly PhaseInterfaceType FrameUpdateInterfaceType = typeof(IFrameUpdateSystem);
    public static readonly PhaseInterfaceType DestroyInterfaceType = typeof(IDestroySystem);

    public static readonly Dictionary<SystemPhase, PhaseInterfaceType> PhaseToInterfaceTypeDict = new()
    {
        { SystemPhase.Awake, AwakeInterfaceType },
        { SystemPhase.LogicUpdate, LogicUpdateInterfaceType },
        { SystemPhase.FrameUpdate, FrameUpdateInterfaceType },
        { SystemPhase.Destroy, DestroyInterfaceType }
    };

    public static PhaseInterfaceType GetInterfaceBySystemPhase(SystemPhase phase)
    {
        return PhaseToInterfaceTypeDict[phase];
    }

    public static IEnumerable<PhaseInterfaceType> AllInterfaceTypes => PhaseToInterfaceTypeDict.Values;

    public Type type;

    public PhaseInterfaceType(Type type)
    {
        this.type = type;
    }

    public static implicit operator PhaseInterfaceType(Type type)
    {
        return new PhaseInterfaceType(type);
    }

    public bool Equals(PhaseInterfaceType other)
    {
        return type == other.type;
    }

    public override bool Equals(object? obj)
    {
        return obj is PhaseInterfaceType other && Equals(other);
    }

    public override int GetHashCode()
    {
        return type != null ? type.GetHashCode() : 0;
    }
}

public enum SystemPhase
{
    Awake,
    LogicUpdate,
    FrameUpdate,
    Destroy
}