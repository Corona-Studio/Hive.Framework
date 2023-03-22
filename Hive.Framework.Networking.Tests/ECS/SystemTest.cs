using Hive.Framework.ECS;
using Hive.Framework.ECS.Attributes.System;
using Hive.Framework.ECS.Component;
using Hive.Framework.ECS.Compositor;
using Hive.Framework.ECS.Entity;
using Hive.Framework.ECS.System.Phases;

namespace Hive.Framework.Networking.Tests.ECS
{
    public class SystemTest
    {
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
                entity.AddComponent(new NumberComponent(2));
            }
        }

        private class AddTwoSystem : ILogicUpdateSystem
        {
            public void OnLogicUpdate(IEntity entity)
            {
                entity.ModifyComponent((ref NumberComponent comp) => { comp.Number += 2; });
            }
        }

        [ExecuteAfter(typeof(AddTwoSystem))]
        private class MultiTwoSystem : ILogicUpdateSystem
        {
            public void OnLogicUpdate(IEntity entity)
            {
                var numberComponent = entity.GetComponent<NumberComponent>();
                if (numberComponent != null)
                {
                    numberComponent.Number *= 2;
                }
            }
        }

        [ExecuteAfter(typeof(AddTwoSystem))]
        [ExecuteBefore(typeof(MinusOneSystem))]
        private class MultiThreeSystem : ILogicUpdateSystem
        {
            public void OnLogicUpdate(IEntity entity)
            {
                var numberComponent = entity.GetComponent<NumberComponent>();
                if (numberComponent != null)
                {
                    numberComponent.Number *= 3;
                }
            }
        }

        [ExecuteAfter(typeof(MultiTwoSystem))]
        private class MinusOneSystem : ILogicUpdateSystem
        {
            public void OnLogicUpdate(IEntity entity)
            {
                var numberComponent = entity.GetComponent<NumberComponent>();
                if (numberComponent != null)
                {
                    numberComponent.Number -= 2;
                }
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
            Assert.True(entity.GetComponent<NumberComponent>().Number == 22);
        }
    }
}