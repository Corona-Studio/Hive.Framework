namespace Hive.Framework.ECS.Entity
{
    public sealed class RootEntity : Entity
    {
        public RootEntity(IECSArch arch)
        {
            ECSArch = arch;
            Parent = null;
        }
    }
}