using System.Net;
using System.Net.Sockets;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Kcp;

namespace Hive.Framework.Networking.Udp
{
    public class KcpSessionCreator<TPackId> : ISessionCreator<KcpSession<TPackId>,Socket> where TPackId : unmanaged
    {
        private readonly IPacketCodec<TPackId> _codec;
        private readonly IDataDispatcher<KcpSession<TPackId>> _dispatcher;

        public KcpSessionCreator(IPacketCodec<TPackId> codec, IDataDispatcher<KcpSession<TPackId>> dispatcher)
        {
            _codec = codec;
            _dispatcher = dispatcher;
        }
        
        public KcpSession<TPackId> CreateSession(Socket socket, IPEndPoint endPoint)
        {
            return null;// new KcpSession<TPackId>(socket,endPoint, _codec, _dispatcher);
        }
    }
}