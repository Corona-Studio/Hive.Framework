using System.Collections.Generic;

namespace Hive.Framework.Codec.Abstractions
{
    public interface ICanRedirectPacket<TId> where TId : unmanaged
    {
        ISet<TId>? RedirectPacketIds { get; set; }
        public bool RedirectReceivedData { get; set; }
    }
}