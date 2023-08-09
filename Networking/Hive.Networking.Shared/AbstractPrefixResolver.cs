using Hive.Framework.Codec.Abstractions;
using System;

namespace Hive.Framework.Networking.Shared;

public abstract class AbstractPrefixResolver : IPacketPrefixResolver
{
    protected ReadOnlySpan<byte> GetAndIncrementIndex(ReadOnlySpan<byte> data, int length, ref int index)
    {
        try
        {
            var result = data.Slice(index, length);

            index += length;

            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public abstract object Resolve(ReadOnlySpan<byte> data, ref int index);
}