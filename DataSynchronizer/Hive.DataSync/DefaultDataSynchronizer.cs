using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hive.DataSync.Abstraction;
using Hive.DataSync.Abstraction.Interfaces;
using Hive.DataSync.Shared.Attributes;
using Hive.DataSync.Shared.Helpers;
using Hive.Framework.Shared.Helpers;

namespace DataSync
{
    public class DefaultDataSynchronizer /*: IDataSynchronizer<ISyncObject>*/
    {
        /*const int MinSyncInterval = 5;

        private readonly ISender _packetSender;
        private readonly ConcurrentDictionary<ushort, Type> _syncObjectTypeMappings = new();
        private readonly ConcurrentDictionary<ushort, List<ISyncObject>> _syncObjectMappings = new();
        private readonly ConcurrentDictionary<ISyncObject, TimeSpan> _syncIntervals = new();
        private readonly ConcurrentDictionary<ISyncObject, DateTime> _lastSyncTimes = new();

        private CancellationTokenSource _cancellationTokenSource = new();

        public DefaultDataSynchronizer(ISender packetSender)
        {
            _packetSender = packetSender;
        }

        public void AddSync(ISyncObject syncObject)
        {
            var syncIntervalAttr = syncObject.GetType().GetCustomAttribute<SetSyncIntervalAttribute>();
            var syncInterval = syncIntervalAttr?.SyncInterval ?? TimeSpan.FromMilliseconds(100);

            if (_syncObjectTypeMappings.TryGetValue(syncObject.ObjectSyncId, out var objectType))
            {
                if (objectType != syncObject.GetType())
                    throw new InvalidOperationException(
                        $"Failed to add sync object {syncObject.GetType().FullName} with id {syncObject.ObjectSyncId} to the mapping dictionary. Please check if the object sync id is duplicated.");
            }
            else
            {
                var succeeded = _syncObjectTypeMappings.TryAdd(syncObject.ObjectSyncId, syncObject.GetType());
                if (!succeeded)
                    throw new InvalidOperationException(
                        $"Failed to add sync object {syncObject.GetType().FullName} with id {syncObject.ObjectSyncId} to the mapping dictionary.");
            }

            _syncIntervals.AddOrUpdate(syncObject, syncInterval, (_, _) => syncInterval);
            _syncObjectMappings.AddOrUpdate(syncObject.ObjectSyncId, new List<ISyncObject> { syncObject }, (_, list) =>
            {
                if(list.Contains(syncObject)) return list;
                
                list.Add(syncObject);
                return list;
            });
        }

        public void RemoveSync(ISyncObject syncObject)
        {
            if(_syncObjectMappings.TryGetValue(syncObject.ObjectSyncId, out var list))
                list.Remove(syncObject);
        }

        public void RemoveSync(ushort objectSyncId)
        {
            _syncObjectMappings.TryRemove(objectSyncId, out _);
        }

        public void Start()
        {
            Stop();

            _cancellationTokenSource = new CancellationTokenSource();
            TaskHelper.ManagedRun(() => UpdateLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }

        private async Task UpdateLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var (_, list) in _syncObjectMappings)
                {
                    foreach (var syncObject in list)
                    {
                        var pendingChanges = syncObject.GetPendingChanges().ToList();

                        if (pendingChanges.Count == 0) continue;

                        var syncInterval = _syncIntervals[syncObject];
                        var currentTime = DateTime.Now;

                        if (!_lastSyncTimes.TryGetValue(syncObject, out var lastSyncTime))
                        {
                            _lastSyncTimes.TryAdd(syncObject, currentTime);
                            continue;
                        }
                        if (lastSyncTime != default && currentTime - lastSyncTime < syncInterval) continue;

                        _lastSyncTimes[syncObject] = currentTime;
                    
                        foreach (var change in pendingChanges)
                        {
                            if (cancellationToken.IsCancellationRequested) return;
                        
                            var packetFlags = change.SyncOptions.ToPacketFlags();
                            await _packetSender.SendAsync(change, packetFlags);
                        }
                    }
                }

                await Task.Delay(70, cancellationToken);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        public void PerformSync(ISyncPacket syncPacket)
        {
            if (!_syncObjectMappings.TryGetValue(syncPacket.ObjectSyncId, out var list)) return;

            foreach (var syncObject in list)
                syncObject.PerformUpdate(syncPacket);
        }*/
    }
}
