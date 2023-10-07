using System;
using System.Collections.Generic;
using System.Linq;

namespace Hive.Common.ECS.System;

public class SystemGraph
{
    private readonly Dictionary<Type, SystemNode> _typeToNodeDict = new();

    public void AddSystemWarp(SystemInstance current)
    {
        var systemNode = new SystemNode(current);

        foreach (var otherNode in _typeToNodeDict.Values)
        {
            if (otherNode.SystemInstance.ExecuteBeforeSystems.Contains(current.SystemType))
                systemNode.Previous.Add(otherNode.SystemInstance.SystemType);

            if (current.ExecuteBeforeSystems.Contains(otherNode.SystemInstance.SystemType))
                otherNode.Previous.Add(current.SystemType);
        }

        _typeToNodeDict.Add(current.SystemType, systemNode);
    }

    public IEnumerable<SystemInstance> GetTopologicalSortedSequence()
    {
        var inDegreeDict = new Dictionary<Type, SystemNode>(_typeToNodeDict);
        var newOrderedSystemList = new List<SystemInstance>();

        try
        {
            while (inDegreeDict.Count > 0)
            {
                var minInDegreePair = inDegreeDict.First(pair => pair.Value.Previous.Count == 0);

                var systemWarp = minInDegreePair.Value.SystemInstance;
                newOrderedSystemList.Add(systemWarp);
                inDegreeDict.Remove(minInDegreePair.Key);

                // 更新当前System的 executeBeforeSystem 的入度

                foreach (var node in inDegreeDict) node.Value.Previous.Remove(systemWarp.SystemType);
            }
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidOperationException("无法对System进行拓扑排序，System之间可能存在循环依赖", e);
        }

        return newOrderedSystemList;
    }

    public SystemNode GetNodeByType(Type type)
    {
        return _typeToNodeDict[type];
    }

    public class SystemNode
    {
        public readonly HashSet<Type> Previous;
        public readonly SystemInstance SystemInstance;

        public SystemNode(SystemInstance instance)
        {
            SystemInstance = instance;
            Previous = new HashSet<Type>(instance.ExecuteAfterSystems);
        }

        public SystemNode(in SystemNode node)
        {
            SystemInstance = node.SystemInstance;
            Previous = new HashSet<Type>(node.Previous);
        }
    }
}