using Hive.Framework.Networking.Shared;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Channels;
using Hive.Framework.Networking.Abstractions;
using Microsoft.Extensions.Logging;

namespace Hive.Framework.Networking.Udp
{
    /// <summary>
    /// 基于 Socket 的 UDP 传输层实现
    /// </summary>
    public sealed class UdpSession : AbstractSession
    {
        private UdpAcceptor _acceptor;


        public UdpSession(int sessionId, IPEndPoint remoteEndPoint,UdpAcceptor udpAcceptor,
            ILogger<UdpSession> logger, IMessageBufferPool messageBufferPool) : base(sessionId, logger, messageBufferPool)
        {
            RemoteEndPoint = remoteEndPoint;
            _acceptor = udpAcceptor;
        }
        
        public override IPEndPoint? LocalEndPoint { get; }

        public override IPEndPoint? RemoteEndPoint { get; }

        public override bool CanSend => true;

        public override bool CanReceive => true;

        public override bool IsConnected => true;

        private readonly byte[] _sendBuffer = new byte[NetworkSetting.DefaultBufferSize];

        private readonly byte[] _receiveBuffer = new byte[NetworkSetting.DefaultBufferSize];
        
        internal event Func<IPEndPoint, ArraySegment<byte>, CancellationToken, ValueTask<int>>? OnSend;

        public override async ValueTask<int> SendOnce(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            if (RemoteEndPoint == null)
                return 0;
            
            data.CopyTo(_sendBuffer);
            var task = OnSend?.Invoke(RemoteEndPoint, _sendBuffer, token) ?? new ValueTask<int>(0);
            return await task;
        }

        private readonly Channel<IMessageBuffer> _messageStreamChannel = Channel.CreateUnbounded<IMessageBuffer>();
        
        internal void OnReceivedAsync(Memory<byte> memory, CancellationToken token)
        {
            var stream = MessageBufferPool.Rent();
            memory.CopyTo(stream.GetMemory());
            stream.Advance(memory.Length);
            _messageStreamChannel.Writer.TryWrite(stream);
        }

        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer, CancellationToken token)
        {
            await _messageStreamChannel.Reader.WaitToReadAsync(token);
            
            if (!_messageStreamChannel.Reader.TryRead(out var stream)) return 0;
            
            var bufferMemory = stream.GetFinalBufferMemory();
            var len = bufferMemory.Length;
            bufferMemory.CopyTo(buffer);
            stream.Dispose();
            return len;
        }
    }
}