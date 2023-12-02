using Hive.Codec.Abstractions;
using Hive.Network.Shared.Session;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace Hive.Both.General.Helpers
{
    public static class SessionExtensions
    {
        public static async Task SendAsync<T>(
            this AbstractSession session,
            IPacketCodec codec,
            T message,
            CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            codec.Encode(message, ms);
            await session.SendAsync(ms, cancellationToken);
        }
    }
}