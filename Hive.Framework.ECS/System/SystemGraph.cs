namespace Hive.Framework.ECS.System
{
    public class SystemGraph
    {
        public class SystemNode
        {
            public readonly HashSet<Type> previous;
            public readonly SystemWarp systemWarp;
            public SystemNode(SystemWarp warp)
            {
                systemWarp = warp;
                previous = new HashSet<Type>(warp.executeAfterSystems);
            }

            public SystemNode(in SystemNode node)
            {
                systemWarp = node.systemWarp;
                previous = new HashSet<Type>(node.previous);
            }
        }

        private readonly Dictionary<Type, SystemNode> typeToNodeDict = new();

        public void AddSystemWarp(SystemWarp current)
        {
            var systemNode = new SystemNode(current);
            
            foreach (var otherNode in typeToNodeDict.Values)
            {
                if (otherNode.systemWarp.executeBeforeSystems.Contains(current.SystemType))
                {
                    systemNode.previous.Add(otherNode.systemWarp.SystemType);
                }

                if (current.executeBeforeSystems.Contains(otherNode.systemWarp.SystemType))
                {
                    otherNode.previous.Add(current.SystemType);
                }
            }
            
            typeToNodeDict.Add(current.SystemType,systemNode);
        }

        public IEnumerable<SystemWarp> GetTopologicalSortedSequence()
        {
            var inDegreeDict = new Dictionary<Type, SystemNode>(typeToNodeDict);
            var newOrderedSystemList = new List<SystemWarp>();

            try
            {
                while (inDegreeDict.Count > 0)
                {
                    var minInDegreePair = inDegreeDict.First(pair => pair.Value.previous.Count == 0);

                    var systemWarp = minInDegreePair.Value.systemWarp;
                    newOrderedSystemList.Add(systemWarp);
                    inDegreeDict.Remove(minInDegreePair.Key);

                    // 更新当前System的 executeBeforeSystem 的入度

                    foreach (var node in inDegreeDict)
                    {
                        node.Value.previous.Remove(systemWarp.SystemType);
                    }
                }
            }catch (InvalidOperationException e)
            {
                throw new InvalidOperationException("无法对System进行拓扑排序，System之间可能存在循环依赖", e);
            }

            return newOrderedSystemList;
        }

        public SystemNode GetNodeByType(Type type)
        {
            return typeToNodeDict[type];
        }
    }
}