using System;
using System.Collections.Generic;

namespace Hive.Codec.Shared;

public class PacketIdMapperOptions
{
    public HashSet<Type> RegisteredPackets { get; } = [];

    public void Register<T>()
    {
        RegisteredPackets.Add(typeof(T));
    }
}