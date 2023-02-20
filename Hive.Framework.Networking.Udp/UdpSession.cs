using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Buffers;
using System.Threading;
using Hive.Framework.Networking.Shared.Helpers;

namespace Hive.Framework.Networking.Udp
{
    /// <summary>
    /// 基于 Socket 的 UDP 传输层实现
    /// </summary>
    public sealed class UdpSession<TId> : AbstractSession<TId, UdpSession<TId>>
    {
        public UdpSession(
            UdpClient socket,
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            UdpConnection = socket;

            RemoteEndPoint = endPoint;
            
            ReaderWriterLock = new ReaderWriterLockSlim();
            DataWriter = new ArrayBufferWriter<byte>();
        }

        public UdpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(endPoint);
        }

        public UdpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(addressWithPort);
        }

        private bool _closed;

        public UdpClient? UdpConnection { get; private set; }
        public ArrayBufferWriter<byte> DataWriter { get; }
        public ReaderWriterLockSlim ReaderWriterLock { get; }

        public override bool CanSend => true;
        public override bool CanReceive => true;
        public override bool IsConnected => true;

        protected override void DispatchPacket(object? packet, Type? packetType = null)
        {
            if (packet == null) return;

            DataDispatcher.Dispatch(this, packet, packetType);
        }

        public override async ValueTask DoConnect()
        {
            // 释放先前的连接
            await DoDisconnect();

            // 创建新连接
            _closed = false;
            UdpConnection = new UdpClient();
        }

        public override ValueTask DoDisconnect()
        {
            if (_closed || UdpConnection == null) return default;

            UdpConnection.Dispose();
            _closed = true;

            return default;
        }

        public override async ValueTask SendOnce(ReadOnlyMemory<byte> data)
        {
            if (UdpConnection == null)
                throw new InvalidOperationException("Socket Init failed!");

            var totalLen = data.Length;
            var sentLen = 0;

            while (sentLen < totalLen)
            {
                var sendThisTime = await UdpConnection.SendAsync(data.ToArray(), data.Length, RemoteEndPoint);
                sentLen += sendThisTime;
            }
        }

        public void AdvanceLengthCanRead(int by) => _lengthCanRead += by;

        private int _currentPosition;
        private long _lengthCanRead;
        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
        {
            if (UdpConnection == null)
                throw new InvalidOperationException("Socket Init failed!");

            ReaderWriterLock.EnterReadLock();

            await SpinWaitAsync.SpinUntil(() => Interlocked.Read(ref _lengthCanRead) != 0);

            var readLength = buffer.Length > _lengthCanRead ? (int)_lengthCanRead : buffer.Length;
            DataWriter.WrittenSpan.Slice(_currentPosition, readLength).CopyTo(buffer.Span);

            ReaderWriterLock.ExitReadLock();

            _currentPosition += readLength;
            _lengthCanRead -= readLength;

            if (readLength == _lengthCanRead && DataWriter.WrittenCount > 10000)
            {
                ReaderWriterLock.EnterWriteLock();

                DataWriter.Clear();
                _currentPosition = 0;
                _lengthCanRead = 0;

                ReaderWriterLock.ExitWriteLock();
            }

            return readLength;
        }

        public override void Dispose()
        {
            base.Dispose();
            DoDisconnect();
        }
    }
}