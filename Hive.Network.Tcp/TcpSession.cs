using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Shared;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Tcp
{
    /// <summary>
    ///     基于 Socket 的 TCP 传输层实现
    /// </summary>
    public sealed class TcpSession : AbstractSession
    {
        public TcpSession(
            int sessionId,
            Socket socket,
            ILogger<TcpSession> logger)
            : base(sessionId, logger)
        {
            Socket = socket;
            socket.ReceiveBufferSize = NetworkSettings.DefaultSocketBufferSize;
        }

        public Socket? Socket { get; private set; }

        public override IPEndPoint? LocalEndPoint => Socket?.LocalEndPoint as IPEndPoint;

        public override IPEndPoint? RemoteEndPoint => Socket?.RemoteEndPoint as IPEndPoint;

        public override bool CanSend => IsConnected;

        public override bool CanReceive => IsConnected;

        public override bool IsConnected => Socket is { Connected: true };

        public event EventHandler<SocketError>? OnSocketError;

        public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            var len = await Socket.SendAsync(data, SocketFlags.None, token);
            if (len == 0) OnSocketError?.Invoke(this, SocketError.ConnectionReset);
            return len;
        }

        /// <summary>
        ///     TCP是基于流的，所以需要自己处理两个报文粘在一起的情况
        /// </summary>
        protected override async Task ReceiveLoop(CancellationToken stoppingToken)
        {
            var receiveBuffer = ArrayPool<byte>.Shared.Rent(NetworkSettings.DefaultBufferSize);
            var segment = new ArraySegment<byte>(receiveBuffer);

            var receivedLen = 0; //当前共接受了多少数据
            var actualLen = 0;
            var isNewPacket = true;

            var offset = 0; //当前接受到的数据在buffer中的偏移量，buffer中的有效数据：buffer[offset..offset+receivedLen]

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!CanReceive)
                    {
                        OnSocketError?.Invoke(this, SocketError.NotConnected);
                        break;
                    }

                    var lenThisTime = await ReceiveOnce(segment[(offset + receivedLen)..], stoppingToken);

                    if (lenThisTime == 0)
                    {
                        OnSocketError?.Invoke(this, SocketError.ConnectionReset);
                        break;
                    }

                    receivedLen += lenThisTime;
                    if (isNewPacket && receivedLen >= NetworkSettings.PacketHeaderLength)
                    {
                        actualLen = BitConverter.ToUInt16(
                            segment[(offset + NetworkSettings.PacketLengthOffset)..]); // 获取实际长度(负载长度)
                        isNewPacket = false;
                    }

                    if (actualLen == 0) continue;

                    /*
#if TRACE
                    if (RemoteEndPoint is IPEndPoint remoteEndPoint)
                        Logger.LogTrace("接收 {RemoteIP}:{RemotePort} 发来的 [{LenThisTime}/{ActualLen}] 字节",
                            remoteEndPoint.Address, remoteEndPoint.Port, lenThisTime, actualLen);
#endif
                    */

                    while (actualLen > 0 && receivedLen >= actualLen) //解决粘包
                    {
                        /*
#if TRACE
                        Logger.LogTrace("集齐 {ActualLen} 字节 开始处理处理数据包", actualLen);
#endif
                        */
                        var bodyLength = actualLen - NetworkSettings.PacketBodyOffset;
                        FireMessageReceived(segment.Slice(offset + NetworkSettings.PacketBodyOffset, bodyLength));

                        offset += actualLen;
                        receivedLen -= actualLen;
                        if (receivedLen >= NetworkSettings.PacketHeaderLength) //还有超过4字节的数据
                        {
                            var headMem = segment.Slice(offset, NetworkSettings.PacketHeaderLength);
                            actualLen = BitConverter.ToUInt16(headMem);
                            // 如果 receivedLen >= actualLen,那么下一次循环会把这个包处理掉
                            // 如果 receivedLen < actualLen,等下一次大循环接收到足够的数据，再处理
                        }
                        else
                        {
                            isNewPacket = true;
                            break;
                        }
                    }

                    if (receivedLen > 0) //没有超过4字节的数据,offset不变，等到下一次Receive的时候继续接收
                        segment.Slice(offset, receivedLen).CopyTo(segment);

                    offset = 0;
                }
            }
            catch (TaskCanceledException)
            {
                Logger.LogInformation("Receive loop canceled, SessionId:{sessionId}", Id);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Receive loop canceled, SessionId:{sessionId}", Id);
            }
            finally
            {
                ReceivingLoopRunning = false;
                ArrayPool<byte>.Shared.Return(receiveBuffer);
            }
        }

        public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            return await Socket.ReceiveAsync(buffer, SocketFlags.None, token);
        }

        public override void Close()
        {
            IsConnected = false;
            Socket?.Close();
            Socket?.Dispose();
            Socket = null;
        }
    }
}