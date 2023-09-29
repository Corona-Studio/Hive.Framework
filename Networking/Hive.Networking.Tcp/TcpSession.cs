using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Microsoft.Extensions.Logging;

namespace Hive.Framework.Networking.Tcp
{
    /// <summary>
    /// 基于 Socket 的 TCP 传输层实现
    /// </summary>
    public sealed class TcpSession : AbstractSession
    {
        public TcpSession(int sessionId, Socket socket, ILogger<TcpSession> logger, IMessageBufferPool messageBufferPool) : base(sessionId, logger, messageBufferPool)
        {
            Socket = socket;
            socket.ReceiveBufferSize = NetworkSetting.DefaultSocketBufferSize;
        }
        
        private readonly IMemoryOwner<byte> _bufferOwner = MemoryPool<byte>.Shared.Rent(NetworkSetting.DefaultSocketBufferSize);

        public Socket? Socket { get; private set; }

        public override IPEndPoint? LocalEndPoint => Socket?.LocalEndPoint as IPEndPoint;

        public override IPEndPoint? RemoteEndPoint => Socket?.RemoteEndPoint as IPEndPoint;

        public override bool CanSend => IsConnected;

        public override bool CanReceive => IsConnected;

        public override bool IsConnected => Socket is { Connected: true };

        public override async ValueTask<int> SendOnce(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            return await Socket.SendAsync(data, SocketFlags.None, cancellationToken: token);
        }

        /// <summary>
        /// TCP是基于流的，所以需要自己处理两个报文粘在一起的情况
        /// </summary>
        protected override async Task ReceiveLoop(CancellationToken token)
        {
            
            var buffer = _bufferOwner.Memory;

            var receivedLen = 0; //当前共接受了多少数据
            var actualLen = 0;
            var isNewPacket = true;

            var offset = 0; //当前接受到的数据在buffer中的偏移量，buffer中的有效数据：buffer[offset..offset+receivedLen]

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!IsConnected || !CanReceive)
                    {
                        await Task.Delay(10, token);
                        continue;
                    }

                    var lenThisTime = await ReceiveOnce(buffer[(offset + receivedLen)..], token);

                    if (lenThisTime == 0)
                    {
                        // Logger.LogError("Received 0 bytes, the buffer may be full");
                        break;
                    }

                    receivedLen += lenThisTime;
                    if (isNewPacket && receivedLen >= NetworkSetting.PacketHeaderLength)
                    {
                        actualLen = BitConverter.ToUInt16(buffer.Span.Slice(offset, NetworkSetting.PacketHeaderLength)); // 获取实际长度(负载长度)
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

                    while (receivedLen >= actualLen) //解决粘包
                    {
                        /*
#if TRACE
                        Logger.LogTrace("集齐 {ActualLen} 字节 开始处理处理数据包", actualLen);
#endif
                        */
                        
                        FireMessageReceived(buffer.Slice(offset+NetworkSetting.PacketHeaderLength, actualLen));

                        offset += actualLen;
                        receivedLen -= actualLen;
                        if (receivedLen >= NetworkSetting.PacketHeaderLength) //还有超过4字节的数据
                        {
                            var headMem = buffer.Slice(offset, NetworkSetting.PacketHeaderLength);
                            actualLen = BitConverter.ToUInt16(headMem.Span);
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
                        buffer.Slice(offset, receivedLen).CopyTo(buffer);

                    offset = 0;
                }
            }
            finally
            {
                ReceivingLoopRunning = false;
                _bufferOwner.Dispose();
            }
        }

        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer, CancellationToken token)
        {
            return await Socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken: token);
        }
    }
}