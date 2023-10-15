using System.Net;

namespace Hive.Server.Common.Abstract;

public interface IServer : IDisposable
{
    /// <summary>
    /// Start the server with an application.
    /// </summary>
    /// <param name="key">Indicates which acceptor service to get from the container.</param>
    /// <param name="ipEndPoint">The IP endpoint to listen on.</param>
    /// <param name="app">The application to host.</param>
    /// <param name="stoppingToken">Indicates if the server startup should be aborted.</param>
    Task StartAsync(string key, IPEndPoint ipEndPoint, IServerApplication app, CancellationToken stoppingToken);
    
    /// <summary>
    /// Stop processing requests and shut down the server, gracefully if possible.
    /// </summary>
    /// <param name="cancellationToken">Indicates if the graceful shutdown should be aborted.</param>
    Task StopAsync(CancellationToken cancellationToken);
}