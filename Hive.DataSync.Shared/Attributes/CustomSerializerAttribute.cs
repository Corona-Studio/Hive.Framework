using System;
using Hive.DataSync.Shared.ObjectSyncPacket;

namespace Hive.DataSync.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CustomSerializerAttribute : Attribute
    {
        public CustomSerializerAttribute(Type type)
        {
            if (!type.IsSubclassOf(typeof(AbstractObjectSyncPacket)))
                throw new ArgumentException($"Type {type} is not a subclass of {nameof(AbstractObjectSyncPacket)}");

            Type = type;
        }

        public Type Type { get; }
    }
}