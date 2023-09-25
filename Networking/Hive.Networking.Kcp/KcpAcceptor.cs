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
    public sealed class KcpAcceptor<TId, TSessionId> : AbstractAcceptor<Socket, KcpSession<TId>, TId, TSessionId>
        where TId : unmanaged
        where TSessionId : unmanaged
    {
        
        private readonly BiDictionary<EndPoint, uint> _convDictionary = new BiDictionary<EndPoint, uint>();
        private readonly BiDictionary<uint, KcpSession<TId>> _convSessionDictionary =
            new BiDictionary<uint, KcpSession<TId>>();

        private Socket? _serverSocket;

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

        public KcpAcceptor(IPEndPoint endPoint, IPacketCodec<TId> codec, IDataDispatcher<KcpSession<TId>> dataDispatcher, IClientManager<TSessionId, KcpSession<TId>> clientManager, ISessionCreator<KcpSession<TId>, Socket> sessionCreator) : base(endPoint, codec, dataDispatcher, clientManager, sessionCreator)
        {
        }

        public override Task<bool> SetupAsync(CancellationToken token)
        {
            try
            {
                _serverSocket = new Socket(EndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                _serverSocket.ReceiveBufferSize = KcpSession<int>.DefaultSocketBufferSize;
                _serverSocket.Bind(EndPoint);

                return Task.FromResult(true);
            }
            catch
            {
                _serverSocket?.Dispose();
                _serverSocket = null;
            }
            return Task.FromResult(false);
        }

        public override Task<bool> CloseAsync(CancellationToken token)
        {
            if (_serverSocket == null) return Task.FromResult(false);

            _serverSocket.Close();
            _serverSocket.Dispose();
            _serverSocket = null;
            
            return Task.FromResult(true);
        }

        public override async ValueTask<bool> DoAcceptAsync(CancellationToken token)
        {
            if (_serverSocket == null) return false;
            if (_serverSocket.Available <= 0) return false;
            
            var buffer = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                EndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
                var received = _serverSocket.ReceiveFrom(buffer, ref endPoint);

                if (received == 0) return false;

                lock (_convSessionDictionary)
                {
                    if (ClientManager.TryGetSession((IPEndPoint)endPoint, out var session))
                    {
                        // 删除临时转发
                        _convSessionDictionary.RemoveByValue(session!);
                        session!.Kcp!.Input(buffer[..received]);
                        return false;
                    }
                }

                // 如果接收到的字节小于 Conv（uint）的长度，则忽略这个连接请求
                if(received < sizeof(uint)) return false;

                var receivedConv = BitConverter.ToUInt32(buffer);

                // 如果接收到的 Conv 是未初始化的 Conv，则服务器返回一个新的 Conv 来建立链接
                if (receivedConv == KcpSession<int>.UnsetConv ||
                    receivedConv == 0)
                {
                    var newConv = GetNewClientConv();
                    var convBytes = BitConverter.GetBytes(newConv);

                    await _serverSocket!.RawSendTo(convBytes, endPoint);

                    // 将暂时生成的 Conv 保存到字典中，以备客户端发起真正连接时验证
                    lock(_convDictionary)
                        _convDictionary.Add(endPoint, newConv);

                    return false;
                }
                else
                {
                    // 如果接收到的 Conv 是可用的 Conv，则尝试连接客户端
                    // 判断在服务器预存的 Conv 是否和客户端送达的一致，如果不一致则无视该连接
                    lock (_convDictionary)
                    {
                        // 如果没有查询到记录，则忽略
                        if (!_convDictionary.TryGetKeyByValue(receivedConv, out var savedEndPoint))
                            return false;
                        // 如果终结点不匹配，则忽略
                        if (!savedEndPoint.Equals(endPoint)) return false;
                    }

                    var convBytes = BitConverter.GetBytes(receivedConv);

                    await _serverSocket!.RawSendTo(convBytes, endPoint);
                }

                // 如果在字典中发现了这个会话，说明 Conv 已经协商成功但是客户端管理器还没有接收到登录报文
                // 在客户端管理器还没有接受该会话时，需要将收到的数据临时转发至 Session
                lock (_convSessionDictionary)
                {
                    if (_convSessionDictionary.TryGetValueByKey(receivedConv, out var savedSession))
                    {
                        savedSession.Kcp!.Input(buffer[..received]);
                        return false;
                    }
                }

                var clientSession = new KcpSession<TId>(_serverSocket, (IPEndPoint)endPoint, receivedConv, Codec,
                    DataDispatcher); // todo SessionCreator.CreateSession(_serverSocket,(IPEndPoint)endPoint);

                lock (_convSessionDictionary)
                    _convSessionDictionary.Add(receivedConv, clientSession);
                ClientManager.AddSession(clientSession);

                await Task.Delay(10, token);
                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}