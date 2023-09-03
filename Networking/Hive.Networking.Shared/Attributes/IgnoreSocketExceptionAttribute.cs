using System;
using System.Net.Sockets;
using Hive.Framework.Shared.Attributes;

namespace Hive.Framework.Networking.Shared.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class IgnoreSocketExceptionAttribute : AbstractIgnoreExceptionAttribute
{
    private readonly SocketError _socketError;

    public IgnoreSocketExceptionAttribute(SocketError socketError) : base(typeof(SocketException))
    {
        _socketError = socketError;
    }

    public override bool IsMatch(Exception exception)
    {
        if(exception is not SocketException socketException)
            return false;

        return socketException.SocketErrorCode == _socketError;
    }
}