using System;

namespace Hive.Framework.Shared.Attributes
{
    public abstract class AbstractIgnoreExceptionAttribute : Attribute
    {
        protected readonly Type ExceptionType;

        public AbstractIgnoreExceptionAttribute(Type exceptionType)
        {
            if (!exceptionType.IsSubclassOf(typeof(Exception)))
                throw new ArgumentException("ExceptionType must be a type of Exception");

            ExceptionType = exceptionType;
        }

        public abstract bool IsMatch(Exception exception);
    }
}
