using Hive.Framework.Networking.Shared.Attributes;
using System.Net.Quic;
using System.Runtime.Versioning;

namespace Hive.Framework.Networking.Quic;

[RequiresPreviewFeatures]
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