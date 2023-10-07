using BenchmarkDotNet.Attributes;
using Hive.Common.ECS;
using Hive.Common.ECS.Attributes.System;
using Hive.Common.ECS.Component;
using Hive.Common.ECS.Compositor;
using Hive.Common.ECS.Entity;
using Hive.Common.ECS.System.Phases;
using Yitter.IdGenerator;

namespace Hive.Benchmark;

public class ECSBenchmark
{
    [Params(10000)] public static int EntityCount;

    private readonly List<object> _temp = new();
    private ECSCore core;

    [GlobalSetup]
    public void Init()
    {
        YitIdHelper.SetIdGenerator(new IdGeneratorOptions(10));
        core = new ECSCore();
        for (var i = 0; i < EntityCount; i++)
        {
            for (var j = 0; j < Random.Shared.Next(256, 1024); j++) _temp.Add(new string('a', 64));
            core.Instantiate<NumberEntityCompositor>();
        }

        core.AddSystem<MinusOneSystem>();
        core.AddSystem<MultiThreeSystem>();
        core.AddSystem<AddTwoSystem>();
        core.AddSystem<MultiTwoSystem>();

        core.SystemManager.RecomputeExecutionOrder();
        core.EntityManager.UpdateBfsEnumerateList();
    }

    public void RecomputeExecutionOrder()
    {
        core.SystemManager.RecomputeExecutionOrder();
    }

    public void UpdateBfsEnumerateList()
    {
        core.EntityManager.UpdateBfsEnumerateList();
    }

    [Benchmark]
    public void ExecuteFixedUpdate()
    {
        core.FixedUpdate();
    }

    private class NumberComponent : IEntityComponent
    {
        public NumberComponent(int number)
        {
            Number = number;
        }

        public int Number { get; set; }
    }

    private class NumberEntityCompositor : AbstractCompositor<ObjectEntity>
    {
        protected override void Composite(ObjectEntity entity)
        {
            entity.AddComponent(new NumberComponent(Random.Shared.Next(2, 1024)));
        }
    }

    private class AddTwoSystem : ILogicUpdateSystem
    {
        public void OnLogicUpdate(IEntity entity)
        {
            entity.UpdateComponent((ref NumberComponent comp) => { comp.Number += 2; });
        }
    }

    [ExecuteAfter(typeof(AddTwoSystem))]
    private class MultiTwoSystem : ILogicUpdateSystem
    {
        public void OnLogicUpdate(IEntity entity)
        {
            entity.UpdateComponent((ref NumberComponent component) => { component.Number *= 2; });
        }
    }

    [ExecuteAfter(typeof(AddTwoSystem))]
    [ExecuteBefore(typeof(MinusOneSystem))]
    private class MultiThreeSystem : ILogicUpdateSystem
    {
        public void OnLogicUpdate(IEntity entity)
        {
            entity.UpdateComponent((ref NumberComponent component) => { component.Number *= 3; });
        }
    }

    [ExecuteAfter(typeof(MultiTwoSystem))]
    private class MinusOneSystem : ILogicUpdateSystem
    {
        public void OnLogicUpdate(IEntity entity)
        {
            entity.UpdateComponent((ref NumberComponent component) => { component.Number -= 2; });
        }
    }
}