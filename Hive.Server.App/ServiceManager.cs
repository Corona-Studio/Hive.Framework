using Hive.Framework.Shared.Collections;

namespace Hive.Server.App;

public class ServiceManager
{
    private readonly ReaderWriterLockSlim _lock = new();
    private Dictionary<string,ServiceAddress> Services { get; } = new();
    private MultiHashSetDictionary<int,string> HostToServiceNames { get; } = new();

    public bool AddService(ServiceAddress serviceAddress)
    {
        if (!_lock.TryEnterWriteLock(1000)) return false;
        
        try
        {
            if (Services.TryAdd(serviceAddress.ServiceName, serviceAddress))
            {
                HostToServiceNames.Add(serviceAddress.HostId, serviceAddress.ServiceName);
                return true;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        
        return false;
    }
    
    public bool RemoveService(string serviceName)
    {
        if (!_lock.TryEnterWriteLock(1000)) return false;
        try
        {
            Services.Remove(serviceName);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        
        return true;

    }
    
    public bool RemoveServiceOfHost(int hostId)
    {
        if (_lock.TryEnterUpgradeableReadLock(1000))
        {
            try
            {
                var serviceNames = HostToServiceNames[hostId];
                if (serviceNames == null) return false;
                
                if (!_lock.TryEnterWriteLock(1000))
                {
                    return false;
                }

                try
                {
                    foreach (var name in serviceNames)
                    {
                        Services.Remove(name);
                    }

                    return true;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
                
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        return false;
    }
}