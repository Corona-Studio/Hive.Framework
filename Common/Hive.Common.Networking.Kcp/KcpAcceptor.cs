using System;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Shared.Collections;

namespace Hive.Framework.Networking.Kcp
{
    public sealed class KcpAcceptor<TId, TSessionId> : AbstractAcceptor<Socket, KcpSession<TId>, TId, TSessionId> where TId : unmanaged
    {
        public KcpAcceptor(
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<KcpSession<TId>> dataDispatcher,
            IClientManager<TSessionId, KcpSession<TId>> clientManager) : base(endPoint, packetCodec, dataDispatcher, clientManager)
        {
        }
        
        private readonly BiDictionary<EndPoint, uint> _convDictionary = new BiDictionary<EndPoint, uint>();
        private readonly BiDictionary<uint, KcpSession<TId>> _convSessionDictionary =
            new BiDictionary<uint, KcpSession<TId>>();

        public Socket? Socket { get; private set; }

        public override void Start()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.Bind(EndPoint);

            TaskHelper.ManagedRun(StartAcceptClient, CancellationTokenSource.Token);
        }

        public override void Stop()
        {
            Socket?.Dispose();
        }

        private uint GetNewClientConv()
        {
            var conv = 0u;

            lock (_convDictionary)
            {
                while (true)
                {
                    ++conv;

                    if (conv == uint.MaxValue)
                        conv = 1;

                    if (conv == KcpSession<int>.UnsetConv)
                        conv++;

                    if (!_convDictionary.ContainsValue(conv))
                        break;
                }
            }

            return conv;
        }

        private async Task StartAcceptClient()
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                await DoAcceptClient(Socket!, CancellationTokenSource.Token);
                await Task.Delay(10);
            }
        }

        public override async ValueTask DoAcceptClient(Socket client, CancellationToken cancellationToken)
        {
            if (client.Available <= 0) return;

            var buffer = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                EndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
                var received = client.ReceiveFrom(buffer, ref endPoint);

                if (received == 0) return;

                lock (_convSessionDictionary)
                {
                    if (ClientManager.TryGetSession((IPEndPoint)endPoint, out var session))
                    {
                        // 删除临时转发
                        _convSessionDictionary.RemoveByValue(session!);
                        session!.Kcp!.Input(buffer[..received]);
                        return;
                    }
                }

                // 如果接收到的字节小于 Conv（uint）的长度，则忽略这个连接请求
                if(received < sizeof(uint)) return;

                var receivedConv = BitConverter.ToUInt32(buffer);

                // 如果接收到的 Conv 是未初始化的 Conv，则服务器返回一个新的 Conv 来建立链接
                if (receivedConv == KcpSession<int>.UnsetConv ||
                    receivedConv == 0)
                {
                    var newConv = GetNewClientConv();
                    var convBytes = BitConverter.GetBytes(newConv);

                    await Socket!.RawSendTo(convBytes, endPoint);

                    // 将暂时生成的 Conv 保存到字典中，以备客户端发起真正连接时验证
                    lock(_convDictionary)
                        _convDictionary.Add(endPoint, newConv);

                    return;
                }
                else
                {
                    // 如果接收到的 Conv 是可用的 Conv，则尝试连接客户端
                    // 判断在服务器预存的 Conv 是否和客户端送达的一致，如果不一致则无视该连接
                    lock (_convDictionary)
                    {
                        // 如果没有查询到记录，则忽略
                        if (!_convDictionary.TryGetKeyByValue(receivedConv, out var savedEndPoint))
                            return;
                        // 如果终结点不匹配，则忽略
                        if (!savedEndPoint.Equals(endPoint)) return;
                    }

                    var convBytes = BitConverter.GetBytes(receivedConv);

                    await Socket!.RawSendTo(convBytes, endPoint);
                }

                // 如果在字典中发现了这个会话，说明 Conv 已经协商成功但是客户端管理器还没有接收到登录报文
                // 在客户端管理器还没有接受该会话时，需要将收到的数据临时转发至 Session
                lock (_convSessionDictionary)
                {
                    if (_convSessionDictionary.TryGetValueByKey(receivedConv, out var savedSession))
                    {
                        savedSession.Kcp!.Input(buffer[..received]);
                        return;
                    }
                }

                var clientSession = new KcpSession<TId>(
                    client,
                    (IPEndPoint)endPoint,
                    receivedConv,
                    PacketCodec,
                    DataDispatcher);

                lock (_convSessionDictionary)
                    _convSessionDictionary.Add(receivedConv, clientSession);
                ClientManager.AddSession(clientSession);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            Stop();
        }
    }
}