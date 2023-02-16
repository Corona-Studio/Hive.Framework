using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Abstractions
{
    public interface IReceiver<TId>
    {
        IDecoder<TId> Decoder { get; }
    }
}