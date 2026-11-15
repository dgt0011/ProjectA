using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

namespace ProjectA.Blog.Web.Services;

// Decides whether a post request should be recorded as a view. The post page itself is always
// served; this only limits what reaches the API's view log, so a scripted flood or a crawler
// can't inflate the counts or grow blog_post_views without bound:
//   - HEAD requests and requests with no user agent are not views;
//   - self-declared crawlers (Googlebot, bingbot, ...) are not counted;
//   - each client IP may record at most PermitsPerMinute views a minute.
// The API additionally ignores repeat views of the same post by the same reader for 30 minutes.
public sealed partial class ViewRecordingPolicy : IDisposable
{
    public const int PermitsPerMinute = 20;

    // Idle per-IP limiters are dropped by the partitioned limiter, so memory stays bounded by the
    // number of IPs active in the last minute or so.
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(
        ip => RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PermitsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    [GeneratedRegex("bot|crawl|spider|slurp|facebookexternalhit|embedly|preview|monitor|uptime|headless", RegexOptions.IgnoreCase)]
    private static partial Regex CrawlerPattern();

    public bool ShouldRecord(HttpRequest request, string? ipAddress)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var userAgent = request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent) || CrawlerPattern().IsMatch(userAgent))
        {
            return false;
        }

        using var lease = _limiter.AttemptAcquire(ipAddress ?? "unknown");
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
