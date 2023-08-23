using System;
using Hive.DataSynchronizer.Shared.UpdateInfo;

namespace Hive.DataSynchronizer.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UseCustomUpdateInfoTypeAttribute : Attribute
    {
        public Type Type { get; }

        public UseCustomUpdateInfoTypeAttribute(Type type)
        {
            if(!type.IsSubclassOf(typeof(AbstractUpdateInfoBase)))
                throw new ArgumentException($"Type {type} is not a subclass of {nameof(AbstractUpdateInfoBase)}");

            Type = type;
        }
    }
}