using System.Threading.Tasks;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 表示一个发送者
    /// </summary>
    /// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
    public interface ISender<TId>
    {
        ValueTask SendAsync<T>(T obj, PacketFlags flags);
    }
}