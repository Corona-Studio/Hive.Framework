using System;
using System.Buffers;

namespace Hive.Framework.Networking.Abstractions;


public interface IMessageStream: IBufferWriter<byte>, IDisposable
{
    ReadOnlyMemory<byte> GetBufferMemory();
    
    ArraySegment<byte> GetArraySegment();
    
    int Length { get; }
}