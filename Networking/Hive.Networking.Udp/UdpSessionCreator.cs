using System.Net;
using System.Net.Sockets;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Udp
{
    public class UdpSessionCreator<TPackId> : ISessionCreator<UdpSession<TPackId>,Socket> where TPackId : unmanaged
    {
        private readonly IPacketCodec<TPackId> _codec;
        private readonly IDataDispatcher<UdpSession<TPackId>> _dispatcher;

        public UdpSessionCreator(IPacketCodec<TPackId> codec, IDataDispatcher<UdpSession<TPackId>> dispatcher)
        {
            _codec = codec;
            _dispatcher = dispatcher;
        }
        
        public UdpSession<TPackId> CreateSession(Socket socket, IPEndPoint endPoint)
        {
            return new UdpSession<TPackId>(socket,endPoint, _codec, _dispatcher);
        }
    }
}