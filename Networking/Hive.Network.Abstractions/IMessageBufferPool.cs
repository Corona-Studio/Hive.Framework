namespace Hive.Network.Abstractions;

public interface IMessageBufferPool
{
    IMessageBuffer Rent(string? tag=null);
    void Free(IMessageBuffer buffer);
}