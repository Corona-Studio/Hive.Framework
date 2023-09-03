﻿using System;
using Hive.DataSynchronizer.Shared.ObjectSyncPacket;

namespace Hive.DataSynchronizer.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CustomSerializerAttribute : Attribute
    {
        public Type Type { get; }

        public CustomSerializerAttribute(Type type)
        {
            if(!type.IsSubclassOf(typeof(AbstractObjectSyncPacket)))
                throw new ArgumentException($"Type {type} is not a subclass of {nameof(AbstractObjectSyncPacket)}");

            Type = type;
        }
    }
}