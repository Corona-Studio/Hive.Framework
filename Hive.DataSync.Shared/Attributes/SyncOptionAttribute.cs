using System;
using Hive.Common.Shared;

namespace Hive.DataSync.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SyncOptionAttribute : Attribute
    {
        public SyncOptions SyncOption { get; }

        public SyncOptionAttribute(SyncOptions syncOption)
        {
            SyncOption = syncOption;
        }
    }
}
