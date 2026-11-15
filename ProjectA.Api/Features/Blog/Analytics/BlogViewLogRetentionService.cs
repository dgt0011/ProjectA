using Dapper;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Analytics;

// Deletes blog_post_views rows older than Blog:ViewLogRetentionDays (default 365, matching the
// dashboard's longest "Last 365 days" range; 0 or less keeps everything). Runs shortly after
// startup and then once a day. The log holds readers' IP addresses, so it shouldn't grow - or be
// kept - forever. blog_posts.post_views (the all-time counter) is unaffected.
public sealed class BlogViewLogRetentionService(
    IDbConnectionFactory connectionFactory,
    IConfiguration configuration,
    ILogger<BlogViewLogRetentionService> logger) : BackgroundService
{
    public const int DefaultRetentionDays = 365;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public int RetentionDays => configuration.GetValue("Blog:ViewLogRetentionDays", DefaultRetentionDays);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (RetentionDays <= 0)
        {
            logger.LogInformation("Blog view log retention is disabled (Blog:ViewLogRetentionDays <= 0).");
            return;
        }

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await PurgeOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    public async Task<int> PurgeOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var deleted = await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM blog_post_views WHERE viewed_at < now() - make_interval(days => @Days);",
                new { Days = RetentionDays },
                commandTimeout: 300,
                cancellationToken: cancellationToken));
            if (deleted > 0)
            {
                logger.LogInformation("Purged {Count} blog view log rows older than {Days} days.", deleted, RetentionDays);
            }

            return deleted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never take the API down over housekeeping; try again tomorrow.
            logger.LogError(ex, "Purging the blog view log failed.");
            return 0;
        }
    }
}
