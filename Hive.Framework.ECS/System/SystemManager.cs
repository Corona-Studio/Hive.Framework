using Hive.Framework.ECS.Entity;
using Hive.Framework.ECS.System.Phases;

namespace Hive.Framework.ECS.System
{
    public class SystemManager : IBelongToECSArch
    {
        public IECSArch Arch { get; }
        
        private readonly Dictionary<Type, SystemWarp> _typeToSystemDict = new();
        
        private readonly Dictionary<PhaseInterfaceType, Dictionary<Type, SystemWarp>> _phaseToSystemWarpDict = new();

        private readonly Dictionary<PhaseInterfaceType, List<SystemWarp>> _phaseToOrderedSystemWarpListDict = new();

        private readonly Dictionary<PhaseInterfaceType, SystemGraph> _phaseToSystemGraphDict = new();

        

        public SystemManager(IECSArch arch)
        {
            Arch = arch;
            foreach (var systemType in PhaseInterfaceType.AllInterfaceTypes)
            {
                _phaseToSystemWarpDict.Add(systemType, new Dictionary<Type, SystemWarp>());
                _phaseToOrderedSystemWarpListDict.Add(systemType, new List<SystemWarp>());
                _phaseToSystemGraphDict.Add(systemType, new SystemGraph());
            }
        }

        public void RegisterSystem(ISystem system)
        {
            var systemWarp = new SystemWarp(system);
            var type = system.GetType();

            if (_typeToSystemDict.ContainsKey(type))
            {
                throw new InvalidOperationException($"System of type '{type.Name}' has already been registered.");
            }
            
            _typeToSystemDict.Add(type, systemWarp);

            var interfaceTypes = type.GetInterfaces();
            foreach (var interfaceType in interfaceTypes)
            {
                if (_phaseToSystemWarpDict.ContainsKey(interfaceType))
                {
                    _phaseToSystemWarpDict[interfaceType].Add(type, systemWarp);
                    _phaseToSystemGraphDict[interfaceType].AddSystemWarp(systemWarp);
                }
            }
        }

        public void RecomputeExecutionOrder()
        {
            // ReSharper disable once HeapView.ObjectAllocation.Possible
            foreach (var systemType in PhaseInterfaceType.AllInterfaceTypes)
            {
                RecomputeExecutionOrder(systemType);
            }
        }

        public void RecomputeExecutionOrder(PhaseInterfaceType interfaceType)
        {
            lock (_phaseToOrderedSystemWarpListDict)
            {
                var sortedSequence = _phaseToSystemGraphDict[interfaceType].GetTopologicalSortedSequence();
                var orderedSystemsOfThisSystemType = _phaseToOrderedSystemWarpListDict[interfaceType];
                orderedSystemsOfThisSystemType.Clear();
                orderedSystemsOfThisSystemType.AddRange(sortedSequence);
            }
        }

        public IEnumerable<SystemWarp> GetOrderedExecutionSequence(PhaseInterfaceType interfaceType)
        {
            return _phaseToOrderedSystemWarpListDict[interfaceType];
        }

        public void ExecuteSystems(SystemPhase phase,List<IEntity> entities)
        {
            
            var interfaceType = PhaseInterfaceType.GetInterfaceBySystemPhase(phase);
            
            if (!_phaseToOrderedSystemWarpListDict.ContainsKey(interfaceType)) return;
            
            var systemWarps = _phaseToOrderedSystemWarpListDict[interfaceType];
            
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < systemWarps.Count; i++)
            {
                var system = systemWarps[i].System;

                try
                {
                    foreach (var entity in entities)
                    {
                        if (!system.EntityFilter(entity)) continue;
                    
                        switch (phase)
                        {
                            case SystemPhase.Awake:
                                ((IAwakeSystem)system).OnAwake(entity);
                                break;
                            case SystemPhase.LogicUpdate:
                                ((ILogicUpdateSystem)system).OnLogicUpdate(entity);
                                break;
                            case SystemPhase.FrameUpdate:
                                ((IFrameUpdateSystem)system).OnFrameUpdate(entity);
                                break;
                            case SystemPhase.Destroy:
                                ((IDestroySystem)system).OnDestroy(entity);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
                        }
                    }
                }
                catch (Exception e)
                {
                    //todo Debug.LogError(e);
                }
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            }
        }
    }
}