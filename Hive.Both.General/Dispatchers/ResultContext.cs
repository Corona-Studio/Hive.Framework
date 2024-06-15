namespace Hive.Both.General.Dispatchers
{
    public readonly struct ResultContext<T>
    {
        public readonly T Message;

        public ResultContext(T message)
        {
            Message = message;
        }

        public static implicit operator ResultContext<T>(T message)
        {
            return new ResultContext<T>(message);
        }
    }
}