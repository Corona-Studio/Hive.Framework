using System;
using System.Collections.Generic;

namespace Hive.Both.General
{
    public readonly struct HandlerId : IEquatable<HandlerId>, IEqualityComparer<HandlerId>
    {
        public readonly int Id;

        public HandlerId(int id)
        {
            Id = id;
        }

        public bool Equals(HandlerId other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return obj is HandlerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public bool Equals(HandlerId x, HandlerId y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(HandlerId obj)
        {
            return obj.Id;
        }

        public static bool operator ==(HandlerId left, HandlerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HandlerId left, HandlerId right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"(HandleId){Id.ToString()}";
        }
    }
}