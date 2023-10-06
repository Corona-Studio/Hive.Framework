using Hive.Common.ECS.Entity;

namespace Hive.Network.Tests.ECS
{
    public class EntityTest
    {
        [Test]
        [Author("Leon")]
        [Description("测试Entity的层序遍历")]
        public void TestEntityBfsEnumerator()
        {
            IEntity root = new RootEntity(null){InstanceId = 0};
            var world = new WorldEntity(){InstanceId = 1};
            world.Parent = root;
            new ObjectEntity() { InstanceId = 2 }.Parent = world;
            new ObjectEntity() { InstanceId = 3 }.Parent = world;
            new ObjectEntity() { InstanceId = 4 }.Parent = world;
            new ObjectEntity() { InstanceId = 5 }.Parent = world;

            new ObjectEntity() { InstanceId = 6 }.Parent = world.Children[1];
            new ObjectEntity() { InstanceId = 7 }.Parent = world.Children[1];
            new ObjectEntity() { InstanceId = 8 }.Parent = world.Children[1];

            new ObjectEntity() { InstanceId = 9 }.Parent = world.Children[3];
            new ObjectEntity() { InstanceId = 10 }.Parent = world.Children[3];
            new ObjectEntity() { InstanceId = 11 }.Parent = world.Children[3];
            
            new ObjectEntity() { InstanceId = 12 }.Parent = world.Children[1].Children[1];
            new ObjectEntity() { InstanceId = 13 }.Parent = world.Children[1].Children[1];
            new ObjectEntity() { InstanceId = 14 }.Parent = world.Children[1].Children[1];

            var correctResult = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

            int curIndex = 0;
            foreach (var entity in root.GetBFSEnumerator())
            {
                if (entity.InstanceId != correctResult[curIndex])
                {
                    Assert.Fail();
                }

                Console.WriteLine(entity.InstanceId);
                curIndex++;
            }
            
            if(curIndex<correctResult.Length)
                Assert.Fail();
            
            Assert.Pass();
        }
    }
}