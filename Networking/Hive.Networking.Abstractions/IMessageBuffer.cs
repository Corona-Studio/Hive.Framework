using System;
using System.Buffers;

namespace Hive.Framework.Networking.Abstractions;


public interface IMessageBuffer: IBufferWriter<byte>, IDisposable
{
    void SetSlice(int offset, int length);
    Memory<byte> GetFinalBufferMemory();
    
    ArraySegment<byte> GetArraySegment();
    
    int Length { get; }
}