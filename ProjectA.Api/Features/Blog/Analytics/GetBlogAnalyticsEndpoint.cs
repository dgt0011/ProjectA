using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Analytics;

public static class GetBlogAnalyticsEndpoint
{
    private const int DefaultLatest = 7;
    private const int MaxLatest = 90;

    public static void MapGetBlogAnalytics(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetBlogAnalytics")
            .WithSummary("Blog dashboard totals and recent post views")
            .WithDescription(
                "Totals (posts, pages, drafts, views) plus the view counts of the 'latest' most " +
                "recently published posts, oldest first, for the dashboard chart - the same data " +
                "Blogifier's dashboard showed.");
    }

    private static async Task<Ok<BlogAnalyticsResponse>> Handle(
        IDbConnectionFactory connectionFactory,
        int? latest,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(latest ?? DefaultLatest, 1, MaxLatest);

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var totals = await connection.QuerySingleAsync<Totals>(new CommandDefinition(
            """
            SELECT
                COUNT(*) FILTER (WHERE post_type = 1)::int AS TotalPosts,
                COUNT(*) FILTER (WHERE post_type = 2)::int AS TotalPages,
                COUNT(*) FILTER (WHERE published_at IS NULL)::int AS TotalDrafts,
                COALESCE(SUM(post_views), 0)::int AS TotalViews
            FROM blog_posts;
            """,
            cancellationToken: cancellationToken));

        var latestViews = (await connection.QueryAsync<PostViews>(new CommandDefinition(
            """
            SELECT title AS Title, slug AS Slug, post_views AS Views, published_at AS PublishedAt
            FROM (
                SELECT * FROM blog_posts
                WHERE post_type = 1 AND published_at IS NOT NULL
                ORDER BY published_at DESC
                LIMIT @Take
            ) latest
            ORDER BY published_at ASC;
            """,
            new { Take = take },
            cancellationToken: cancellationToken))).ToList();

        return TypedResults.Ok(new BlogAnalyticsResponse(
            totals.TotalPosts, totals.TotalPages, totals.TotalDrafts, totals.TotalViews, take, latestViews));
    }

    internal sealed record Totals(int TotalPosts, int TotalPages, int TotalDrafts, int TotalViews);

    // Shapes returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record PostViews(string Title, string Slug, int Views, DateTime PublishedAt);

    public sealed record BlogAnalyticsResponse(
        int TotalPosts,
        int TotalPages,
        int TotalDrafts,
        int TotalViews,
        int Latest,
        IReadOnlyList<PostViews> LatestPostViews);
}
