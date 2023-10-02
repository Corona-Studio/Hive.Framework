using System;
using System.Buffers;

namespace Hive.Network.Abstractions;


public interface IMessageBuffer: IBufferWriter<byte>, IDisposable
{
    Memory<byte> GetFinalBufferMemory();
    
    ArraySegment<byte> GetArraySegment();
    
    int Length { get; }

    void Free();

    void IDisposable.Dispose()
    {
        Free();
    }
}