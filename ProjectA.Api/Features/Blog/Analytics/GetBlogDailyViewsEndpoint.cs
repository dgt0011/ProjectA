using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Analytics;

// Views per day (from the blog_post_views log) for one post/page or the whole blog, plus the
// visitors (IP address + user agent) behind one day's views. Days are UTC calendar days.
public static class GetBlogDailyViewsEndpoint
{
    private const int DefaultDays = 30;
    private const int MaxDays = 365;
    private const int MaxVisitors = 500;

    public static void MapGetBlogDailyViews(this RouteGroupBuilder group)
    {
        group.MapGet("daily", HandleDaily)
            .WithName("GetBlogDailyViews")
            .WithSummary("Views per day for a post/page or the whole blog")
            .WithDescription(
                "Views per UTC day for the last 'days' days (default 30, max 365) including today, " +
                "zero-filled, oldest first. 'postId' narrows it to one post or page; without it the " +
                "whole blog is counted. Also lists every published post and page (plus any unpublished one " +
                "with views in the period) with its views in " +
                "the same period (most viewed first) so callers can offer a post picker.");

        group.MapGet("daily/{day}", HandleVisitors)
            .WithName("GetBlogDailyVisitors")
            .WithSummary("Who viewed the blog on one day")
            .WithDescription(
                "The views of one UTC day ('yyyy-MM-dd') grouped by IP address and user agent, most " +
                "views first (at most 500 groups; TotalViews and TotalVisitors count the whole day). " +
                "'postId' narrows it to one post or page.");
    }

    private static async Task<Ok<DailyViewsResponse>> HandleDaily(
        IDbConnectionFactory connectionFactory,
        int? days,
        long? postId,
        CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(days ?? DefaultDays, 1, MaxDays);
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(1 - count);
        var range = new
        {
            From = from.ToDateTime(TimeOnly.MinValue),
            To = to.ToDateTime(TimeOnly.MinValue),
            FromUtc = ToUtc(from),
            ToUtc = ToUtc(to.AddDays(1)),
            PostId = postId
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var dayRows = await connection.QueryAsync<DayRow>(new CommandDefinition(
            """
            WITH v AS (
                SELECT (viewed_at AT TIME ZONE 'UTC')::date AS day
                FROM blog_post_views
                WHERE viewed_at >= @FromUtc AND viewed_at < @ToUtc
                  AND (CAST(@PostId AS bigint) IS NULL OR post_id = @PostId)
            )
            SELECT d::timestamp AS Day, COUNT(v.day)::int AS Views
            FROM generate_series(CAST(@From AS date), CAST(@To AS date), interval '1 day') AS d
            LEFT JOIN v ON v.day = d::date
            GROUP BY d
            ORDER BY d;
            """,
            range,
            cancellationToken: cancellationToken));

        var posts = await connection.QueryAsync<PostTotal>(new CommandDefinition(
            """
            SELECT p.id AS Id, p.title AS Title, p.post_type AS PostType, COUNT(v.id)::int AS Views
            FROM blog_posts p
            LEFT JOIN blog_post_views v
                   ON v.post_id = p.id AND v.viewed_at >= @FromUtc AND v.viewed_at < @ToUtc
            GROUP BY p.id, p.title, p.post_type
            -- Published posts, plus any post (e.g. since unpublished) whose views are in the
            -- daily series, so the per-post totals always add up to the chart.
            HAVING bool_or(p.published_at IS NOT NULL) OR COUNT(v.id) > 0
            ORDER BY Views DESC, p.title;
            """,
            range,
            cancellationToken: cancellationToken));

        var dayList = dayRows.Select(r => new DayViews(DateOnly.FromDateTime(r.Day), r.Views)).ToList();
        return TypedResults.Ok(new DailyViewsResponse(
            postId, from, to, dayList.Sum(d => d.Views), dayList, posts.ToList()));
    }

    private static async Task<Ok<DailyVisitorsResponse>> HandleVisitors(
        DateOnly day,
        long? postId,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var range = new { FromUtc = ToUtc(day), ToUtc = ToUtc(day.AddDays(1)), PostId = postId, Limit = MaxVisitors };
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        // Counted separately: the visitor list below is capped, so summing it could undercount.
        var totals = await connection.QuerySingleAsync<(int TotalViews, int TotalVisitors)>(new CommandDefinition(
            """
            SELECT COUNT(*)::int, COUNT(DISTINCT COALESCE(host(ip_address), '') || E'\n' || COALESCE(user_agent, ''))::int
            FROM blog_post_views
            WHERE viewed_at >= @FromUtc AND viewed_at < @ToUtc
              AND (CAST(@PostId AS bigint) IS NULL OR post_id = @PostId);
            """,
            range,
            cancellationToken: cancellationToken));

        var visitors = (await connection.QueryAsync<Visitor>(new CommandDefinition(
            """
            SELECT host(v.ip_address) AS IpAddress,
                   v.user_agent AS UserAgent,
                   COUNT(*)::int AS Views,
                   MIN(v.viewed_at) AS FirstViewedAt,
                   MAX(v.viewed_at) AS LastViewedAt,
                   string_agg(DISTINCT p.title, ', ') AS Posts
            FROM blog_post_views v
            JOIN blog_posts p ON p.id = v.post_id
            WHERE v.viewed_at >= @FromUtc AND v.viewed_at < @ToUtc
              AND (CAST(@PostId AS bigint) IS NULL OR v.post_id = @PostId)
            GROUP BY v.ip_address, v.user_agent
            ORDER BY Views DESC, LastViewedAt DESC
            LIMIT @Limit;
            """,
            range,
            cancellationToken: cancellationToken))).ToList();

        return TypedResults.Ok(new DailyVisitorsResponse(postId, day, totals.TotalViews, totals.TotalVisitors, visitors));
    }

    private static DateTime ToUtc(DateOnly day) => DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    internal sealed record DayRow(DateTime Day, int Views);

    // Shapes returned to callers of these endpoints - owned by this slice, not shared.
    public sealed record DayViews(DateOnly Day, int Views);

    public sealed record PostTotal(long Id, string Title, short PostType, int Views);

    public sealed record DailyViewsResponse(
        long? PostId,
        DateOnly From,
        DateOnly To,
        int TotalViews,
        IReadOnlyList<DayViews> Days,
        IReadOnlyList<PostTotal> Posts);

    public sealed record Visitor(
        string? IpAddress,
        string? UserAgent,
        int Views,
        DateTime FirstViewedAt,
        DateTime LastViewedAt,
        string Posts);

    public sealed record DailyVisitorsResponse(
        long? PostId,
        DateOnly Day,
        int TotalViews,
        // Distinct IP address + user agent combinations; Visitors holds at most 500 of them.
        int TotalVisitors,
        IReadOnlyList<Visitor> Visitors);
}
