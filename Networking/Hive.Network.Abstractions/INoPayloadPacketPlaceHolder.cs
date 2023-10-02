namespace Hive.Network.Abstractions;

/// <summary>
/// 这个是用于标记无负载的封包占位符，用于在数据分发器中指示这种特殊的封包
/// </summary>
public interface INoPayloadPacketPlaceHolder
{
    
}

public class NoPayloadPacketPlaceHolder : INoPayloadPacketPlaceHolder
{
    public static NoPayloadPacketPlaceHolder Empty { get; } = new();

    private NoPayloadPacketPlaceHolder()
    {
    }
}