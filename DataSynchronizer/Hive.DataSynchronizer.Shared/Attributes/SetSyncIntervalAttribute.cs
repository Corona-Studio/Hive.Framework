using System;

namespace Hive.DataSynchronizer.Shared.Attributes
{
    /// <summary>
    /// 用于指示数据同步器的同步间隔，最低值为 5ms
    /// <para>默认值为 100ms</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SetSyncIntervalAttribute : Attribute
    {
        public TimeSpan SyncInterval { get; }

        /// <summary>
        /// 更新间隔，毫秒为单位
        /// </summary>
        /// <param name="syncInterval"></param>
        public SetSyncIntervalAttribute(double syncInterval)
        {
            SyncInterval = TimeSpan.FromMilliseconds(syncInterval < 5 ? 5 : syncInterval);
        }
    }
}