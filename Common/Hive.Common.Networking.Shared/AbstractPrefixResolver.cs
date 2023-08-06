using Hive.Framework.Codec.Abstractions;
using System;

namespace Hive.Framework.Networking.Shared;

public abstract class AbstractPrefixResolver : IPacketPrefixResolver
{
    protected ReadOnlySpan<byte> GetAndIncrementIndex(ReadOnlySpan<byte> data, int length, ref int index)
    {
        var result = data[index..(index + length)];

        index += length;

        return result;
    }

    public abstract object Resolve(ReadOnlySpan<byte> data, ref int index);
}