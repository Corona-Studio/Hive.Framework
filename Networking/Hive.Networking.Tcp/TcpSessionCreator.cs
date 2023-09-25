using System.Net;
using System.Net.Sockets;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Tcp
{
    public class TcpSessionCreator<TPackId> : ISessionCreator<TcpSession<TPackId>,Socket> where TPackId : unmanaged
    {
        private readonly IPacketCodec<TPackId> _codec;
        private readonly IDataDispatcher<TcpSession<TPackId>> _dispatcher;

        public TcpSessionCreator(IPacketCodec<TPackId> codec, IDataDispatcher<TcpSession<TPackId>> dispatcher)
        {
            _codec = codec;
            _dispatcher = dispatcher;
        }

        public TcpSession<TPackId> CreateSession(Socket socket, IPEndPoint endPoint)
        {
            return new TcpSession<TPackId>(socket, _codec, _dispatcher);
        }
    }
}