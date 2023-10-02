using Hive.Framework.Networking.Shared.Attributes;
using System.Net.Quic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hive.Framework.Shared.Attributes;

namespace Hive.Framework.Networking.Quic;

[RequiresPreviewFeatures]
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class IgnoreQuicExceptionAttribute : AbstractIgnoreExceptionAttribute
{
    private readonly QuicError _quicError;

    public IgnoreQuicExceptionAttribute(QuicError quicError) : base(typeof(QuicException))
    {
        _quicError = quicError;
    }

    public override bool IsMatch(Exception exception)
    {
        if (exception is not QuicException quicException)
            return false;

        return quicException.QuicError == _quicError;
    }
}