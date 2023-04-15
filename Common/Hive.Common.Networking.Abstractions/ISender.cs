using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 表示一个发送者
    /// </summary>
    /// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
    public interface ISender<TId>
    {
        void Send<T>(T obj);
    }
}