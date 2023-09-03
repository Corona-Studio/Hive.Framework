using System;
using Hive.Framework.Shared.Attributes;

namespace Hive.Framework.Networking.Shared.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class IgnoreExceptionAttribute : AbstractIgnoreExceptionAttribute
{
    public IgnoreExceptionAttribute(Type exceptionType) : base(exceptionType)
    {
    }

    public override bool IsMatch(Exception exception)
    {
        return exception.GetType() == ExceptionType;
    }
}