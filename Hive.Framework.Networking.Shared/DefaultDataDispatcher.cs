using System;
using System.Collections.Generic;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Shared
{
    public class DefaultDataDispatcher<TSender> : IDataDispatcher<TSender>
    {
        public Dictionary<Type, IDataDispatcher<TSender>.CallbackWarp> CallbackDictionary { get; } = new();
    }
}