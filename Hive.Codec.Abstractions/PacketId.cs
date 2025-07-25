﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Hive.Codec.Abstractions
{
    public struct PacketId : IEquatable<PacketId>, IEqualityComparer<PacketId>
    {
#if PACKET_ID_INT
        public int Id;

        public static implicit operator int(PacketId packetId)
        {
            return packetId.Id;
        }

        public static implicit operator PacketId(int packetId)
        {
            return new PacketId() { Id = packetId };
        }
        public static int Size => sizeof(int);
        
        public static PacketId From(ReadOnlySequence<byte> buffer)
        {
            return new PacketId()
            {
                Id = BitConverter.ToInt32(buffer.FirstSpan)
            };
        }
#else
        public ushort Id;

        public static implicit operator int(PacketId packetId)
        {
            return packetId.Id;
        }

        public static implicit operator PacketId(ushort packetId)
        {
            return new PacketId { Id = packetId };
        }

        public static int Size => sizeof(ushort);

        public static PacketId From(ReadOnlySequence<byte> buffer)
        {
            var reader = new SequenceReader<byte>(buffer);

            if (!reader.TryReadLittleEndian(out short id))
            {
                throw new InvalidOperationException("Failed to read packet id");
            }
            
            return new PacketId
            {
                Id = Unsafe.ReadUnaligned<ushort>(ref Unsafe.As<short, byte>(ref id))
            };
        }
#endif
        public int WriteTo(Span<byte> buffer)
        {
            if (BitConverter.TryWriteBytes(buffer, Id)) return Size;
            return 0;
        }

        public bool Equals(PacketId other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return obj is PacketId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public bool Equals(PacketId x, PacketId y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(PacketId obj)
        {
            return obj.Id;
        }

        public override string ToString()
        {
            return $"(PacketId){Id.ToString()}";
        }
    }
}