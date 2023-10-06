using Hive.Common.ECS;
using Hive.Common.ECS.Attributes.System;
using Hive.Common.ECS.Component;
using Hive.Common.ECS.Compositor;
using Hive.Common.ECS.Entity;
using Hive.Common.ECS.System.Phases;
using Yitter.IdGenerator;

namespace Hive.Network.Tests.ECS
{
    public class SystemTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            YitIdHelper.SetIdGenerator(new IdGeneratorOptions());
        }
        
        private struct NumberComponent : IEntityComponent
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
                entity.AddComponent(new NumberComponent(2));
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
                entity.UpdateComponent((ref NumberComponent component) =>
                {
                    component.Number *= 2;
                });
            }
        }

        [ExecuteAfter(typeof(AddTwoSystem))]
        [ExecuteBefore(typeof(MinusOneSystem))]
        private class MultiThreeSystem : ILogicUpdateSystem
        {
            public void OnLogicUpdate(IEntity entity)
            {
                entity.UpdateComponent((ref NumberComponent component) =>
                {
                    component.Number *= 3;
                });
            }
        }

        [ExecuteAfter(typeof(MultiTwoSystem))]
        private class MinusOneSystem : ILogicUpdateSystem
        {
            public void OnLogicUpdate(IEntity entity)
            {
                entity.UpdateComponent((ref NumberComponent component) =>
                {
                    component.Number -= 2;
                });
            }
        }

        [Test]
        public void TestSystemLogicUpdate()
        {
            var core = new ECSCore();
            core.Instantiate<NumberEntityCompositor>();

            core.AddSystem<MinusOneSystem>();
            core.AddSystem<MultiThreeSystem>();
            core.AddSystem<AddTwoSystem>();
            core.AddSystem<MultiTwoSystem>();
            core.SystemManager.RecomputeExecutionOrder();
            core.EntityManager.UpdateBfsEnumerateList();
            core.FixedUpdate();

            var entity = core.EntityManager.EntityBfsEnumerateList.First(entity => entity is ObjectEntity);
            Assert.That(entity.GetComponent<NumberComponent>().Number == 22, Is.True);
        }
    }
}