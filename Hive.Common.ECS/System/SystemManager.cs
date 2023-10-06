using System;
using System.Collections.Generic;
using Hive.Common.ECS.Entity;
using Hive.Common.ECS.System.Phases;
using Hive.Common.Shared.Collections;

namespace Hive.Common.ECS.System
{
    public class SystemManager : IBelongToECSArch
    {
        public IECSArch Arch { get; }
        
        private readonly Dictionary<Type, SystemInstance> _typeToSystemDict = new();
        
        private readonly Dictionary<PhaseInterfaceType, Dictionary<Type, SystemInstance>> _phaseToSystemInstDict = new();

        private readonly MultiDictionary<PhaseInterfaceType, SystemInstance> _phaseToSortedSystemInstList = new();

        private readonly Dictionary<PhaseInterfaceType, SystemGraph> _phaseToSystemGraph = new();

        

        public SystemManager(IECSArch arch)
        {
            Arch = arch;
            foreach (var systemType in PhaseInterfaceType.AllInterfaceTypes)
            {
                _phaseToSystemInstDict.Add(systemType, new Dictionary<Type, SystemInstance>());
                _phaseToSortedSystemInstList.Add(systemType, new List<SystemInstance>());
                _phaseToSystemGraph.Add(systemType, new SystemGraph());
            }
        }

        public void RegisterSystem(ISystem system)
        {
            var systemWarp = new SystemInstance(system);
            var type = system.GetType();

            if (_typeToSystemDict.ContainsKey(type))
            {
                throw new InvalidOperationException($"System of type '{type.Name}' has already been registered.");
            }
            
            _typeToSystemDict.Add(type, systemWarp);

            var interfaceTypes = type.GetInterfaces();
            foreach (var interfaceType in interfaceTypes)
            {
                if (_phaseToSystemInstDict.ContainsKey(interfaceType))
                {
                    _phaseToSystemInstDict[interfaceType].Add(type, systemWarp);
                    _phaseToSystemGraph[interfaceType].AddSystemWarp(systemWarp);
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
            lock (_phaseToSortedSystemInstList)
            {
                var sortedSequence = _phaseToSystemGraph[interfaceType].GetTopologicalSortedSequence();
                var orderedSystemsOfThisSystemType = _phaseToSortedSystemInstList[interfaceType];
                orderedSystemsOfThisSystemType.Clear();
                orderedSystemsOfThisSystemType.AddRange(sortedSequence);
            }
        }

        public IEnumerable<SystemInstance> GetOrderedExecutionSequence(PhaseInterfaceType interfaceType)
        {
            return _phaseToSortedSystemInstList[interfaceType];
        }
        
        public void ExecuteSystems(SystemPhase phase)
        {
            var interfaceType = PhaseInterfaceType.GetInterfaceBySystemPhase(phase);
            if (_phaseToSortedSystemInstList.TryGetValue(interfaceType,out var instances))
            {
                foreach (var inst in instances)
                {
                    inst.Execute(phase);
                }
            }
        }

        public void ExecuteSystems(SystemPhase phase,List<IEntity> entities)
        {
            
            var interfaceType = PhaseInterfaceType.GetInterfaceBySystemPhase(phase);
            
            if (!_phaseToSortedSystemInstList.ContainsKey(interfaceType)) return;
            
            var systemWarps = _phaseToSortedSystemInstList[interfaceType];
            
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
                    Console.WriteLine(e);
                    //todo Debug.LogError(e);
                }
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            }
        }
    }
}