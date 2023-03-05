using System;

namespace Hive.Framework.Codec.Abstractions
{
    public interface ICanRedirectPacket<TId> where TId : unmanaged
    {
        TId[]? ExcludeRedirectPacketIds { get; set; }
        public bool RedirectReceivedData { get; set; }
    }
}