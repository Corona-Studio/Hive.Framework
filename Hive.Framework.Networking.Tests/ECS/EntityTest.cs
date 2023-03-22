using Hive.Framework.ECS.Entity;

namespace Hive.Framework.Networking.Tests.ECS
{
    public class EntityTest
    {
        [Test]
        [Author("Leon")]
        [Description("测试Entity的层序遍历")]
        public void TestEntityBfsEnumerator()
        {
            IEntity root = new RootEntity(null){InstanceID = 0};
            var world = new WorldEntity(){InstanceID = 1};
            world.Parent = root;
            new ObjectEntity() { InstanceID = 2 }.Parent = world;
            new ObjectEntity() { InstanceID = 3 }.Parent = world;
            new ObjectEntity() { InstanceID = 4 }.Parent = world;
            new ObjectEntity() { InstanceID = 5 }.Parent = world;

            new ObjectEntity() { InstanceID = 6 }.Parent = world.Children[1];
            new ObjectEntity() { InstanceID = 7 }.Parent = world.Children[1];
            new ObjectEntity() { InstanceID = 8 }.Parent = world.Children[1];

            new ObjectEntity() { InstanceID = 9 }.Parent = world.Children[3];
            new ObjectEntity() { InstanceID = 10 }.Parent = world.Children[3];
            new ObjectEntity() { InstanceID = 11 }.Parent = world.Children[3];
            
            new ObjectEntity() { InstanceID = 12 }.Parent = world.Children[1].Children[1];
            new ObjectEntity() { InstanceID = 13 }.Parent = world.Children[1].Children[1];
            new ObjectEntity() { InstanceID = 14 }.Parent = world.Children[1].Children[1];

            var correctResult = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

            int curIndex = 0;
            foreach (var entity in root.GetBFSEnumerator())
            {
                if (entity.InstanceID != correctResult[curIndex])
                {
                    Assert.Fail();
                }

                Console.WriteLine(entity.InstanceID);
                curIndex++;
            }
            
            if(curIndex<correctResult.Length)
                Assert.Fail();
            
            Assert.Pass();
        }
    }
}