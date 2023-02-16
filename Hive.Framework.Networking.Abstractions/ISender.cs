using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Abstractions
{
    public interface ISender<TId>
    {
        IEncoder<TId> Encoder { get; }

        void Send<T>(T obj);
    }
}