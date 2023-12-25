using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hive.Server.App;

public class TestService : BackgroundService
{
    private readonly ILogger<TestService> _logger;

    public TestService(ILogger<TestService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogRunningTime(DateTimeOffset.Now);
            await Task.Delay(1_000, stoppingToken);
        }
    }
}

internal static partial class TestServiceLoggers
{
    [LoggerMessage(LogLevel.Information, "Worker running at: {Time}")]
    public static partial void LogRunningTime(this ILogger logger, DateTimeOffset time);
}