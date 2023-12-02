using Hive.Common.ECS.Attributes.System;
using Hive.Common.ECS.Entity;
using Hive.Common.ECS.System;
using Hive.Common.ECS.System.Phases;

namespace Hive.Network.Tests.ECS;

public class SystemManagerTest
{
    // A Test behaves as an ordinary method
    [Test]
    public void TestExecutionOrderComputation()
    {
        var systemManager = new SystemManager(null);
        var systems = new IAwakeSystem[]
        {
            new TestSystem1(),
            new TestSystem2(),
            new TestSystem3(),
            new TestSystem4()
        };

        foreach (var system in systems) systemManager.RegisterSystem(system);

        systemManager.RecomputeExecutionOrder();

        var orderedExecutionSequence = systemManager.GetOrderedExecutionSequence(typeof(IAwakeSystem)).ToList();

        var indexOfSystem =
            orderedExecutionSequence.ToDictionary(warp => warp.System, warp => orderedExecutionSequence.IndexOf(warp));

        Assert.That(indexOfSystem[systems[3]] > indexOfSystem[systems[1]] &&
                    indexOfSystem[systems[3]] < indexOfSystem[systems[2]]);
    }

    private class TestSystem1 : IAwakeSystem
    {
        public void OnAwake(IEntity entity)
        {
        }
    }

    [ExecuteBefore(typeof(TestSystem3))]
    private class TestSystem2 : IAwakeSystem
    {
        public void OnAwake(IEntity entity)
        {
        }
    }

    [ExecuteAfter(typeof(TestSystem2))]
    private class TestSystem3 : IAwakeSystem
    {
        public void OnAwake(IEntity entity)
        {
        }
    }

    [ExecuteBefore(typeof(TestSystem3))]
    [ExecuteAfter(typeof(TestSystem2))]
    private class TestSystem4 : IAwakeSystem
    {
        public void OnAwake(IEntity entity)
        {
        }
    }
}