namespace Hive.Framework.Networking.Abstractions;

/// <summary>
/// 指示客户端管理器是否应该在断联后销毁会话
/// </summary>
public interface IShouldDestroySession
{
    public bool ShouldDestroyAfterDisconnected { get; }
}