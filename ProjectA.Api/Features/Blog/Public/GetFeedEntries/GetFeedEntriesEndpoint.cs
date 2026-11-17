using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Public.GetFeedEntries;

public static class GetFeedEntriesEndpoint
{
    private const int DefaultCount = 10;
    private const int MaxCount = 50;

    public static void MapGetFeedEntries(this RouteGroupBuilder group)
    {
        group.MapGet("feed", Handle)
            .WithName("GetFeedEntries")
            .WithSummary("Latest published posts for the RSS feed")
            .WithDescription("Newest first, with full Markdown content (the feed carries rendered post bodies).");

        group.MapGet("sitemap", HandleSitemap)
            .WithName("GetSitemapEntries")
            .WithSummary("Every published post and page for sitemap.xml");
    }

    private static async Task<Ok<List<FeedEntryResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        int? count,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(count ?? DefaultCount, 1, MaxCount);

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await PublishedPosts.QueryAsync(
            connection, "p.post_type = 1", "ORDER BY p.published_at DESC, p.id DESC LIMIT @Take", new { Take = take }, cancellationToken);
        var categories = await BlogPostCategoryLinks.GetForPostsAsync(connection, rows.Select(r => r.id).ToList(), cancellationToken);

        var entries = rows
            .Select(r => new FeedEntryResponse(
                r.title, r.slug, r.description, r.content, r.published_at, r.author_name,
                categories[r.id].Select(c => c.Title).ToList()))
            .ToList();

        return TypedResults.Ok(entries);
    }

    private static async Task<Ok<List<SitemapEntryResponse>>> HandleSitemap(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var entries = await connection.QueryAsync<SitemapEntryResponse>(new CommandDefinition(
            $"""
            SELECT p.slug AS Slug, p.post_type AS PostType, p.published_at AS PublishedAt,
                   COALESCE(p.date_modified, p.published_at) AS LastModified
            FROM blog_posts p
            WHERE {PublishedPosts.VisibleFilter}
            ORDER BY p.published_at DESC;
            """,
            cancellationToken: cancellationToken));

        return TypedResults.Ok(entries.ToList());
    }

    // Shapes returned to callers of these endpoints - owned by this slice, not shared.
    public sealed record FeedEntryResponse(
        string Title,
        string Slug,
        string Description,
        string Content,
        DateTime PublishedAt,
        string AuthorName,
        IReadOnlyList<string> Categories);

    public sealed record SitemapEntryResponse(string Slug, short PostType, DateTime PublishedAt, DateTime LastModified);
}
