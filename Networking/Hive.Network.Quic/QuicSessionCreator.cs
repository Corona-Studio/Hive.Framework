using System.Net;
using System.Net.Quic;
using System.Net.Sockets;
using System.Runtime.Versioning;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Quic;

namespace Hive.Framework.Networking.Udp
{
    [RequiresPreviewFeatures]
    public class QuicSessionCreator<TPackId> : ISessionCreator<QuicSession<TPackId>,QuicConnection> where TPackId : unmanaged
    {
        private readonly IPacketCodec<TPackId> _codec;
        private readonly IDataDispatcher<QuicSession<TPackId>> _dispatcher;

        public QuicSessionCreator(IPacketCodec<TPackId> codec, IDataDispatcher<QuicSession<TPackId>> dispatcher)
        {
            _codec = codec;
            _dispatcher = dispatcher;
        }
        
        public QuicSession<TPackId> CreateSession(QuicConnection connection, IPEndPoint endPoint)
        {
            var stream = connection.AcceptInboundStreamAsync().AsTask().Result;
            return new QuicSession<TPackId>(connection,stream, _codec, _dispatcher);
        }
    }
}