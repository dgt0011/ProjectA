using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Public.GetPublishedPostBySlug;

public static class GetPublishedPostBySlugEndpoint
{
    private const int RelatedPostCount = 3;

    public static void MapGetPublishedPostBySlug(this RouteGroupBuilder group)
    {
        group.MapGet("published/{slug}", Handle)
            .WithName("GetPublishedPostBySlug")
            .WithSummary("Get a published post or page by its slug")
            .WithDescription(
                "Returns the post's Markdown content plus, for posts (not pages), the previous/next " +
                "post links and up to three related posts. Drafts are never returned here.");
    }

    private static async Task<Results<Ok<PublishedPostResponse>, ProblemHttpResult>> Handle(
        string slug,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var row = (await PublishedPosts.QueryAsync(
            connection, "p.slug = @Slug", "LIMIT 1", new { Slug = slug }, cancellationToken)).FirstOrDefault();

        if (row is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Post not found",
                detail: $"No published post exists with slug '{slug}'.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        var authorBio = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT u.bio FROM blog_posts p JOIN users u ON u.id = p.author_id WHERE p.id = @Id;",
            new { Id = row.id },
            cancellationToken: cancellationToken));

        var ownCategories = await BlogPostCategoryLinks.GetForPostsAsync(connection, [row.id], cancellationToken);
        var post = PublishedPosts.ToSummary(row, ownCategories[row.id]);

        if (row.post_type != BlogPostTypes.Post)
        {
            return TypedResults.Ok(new PublishedPostResponse(post, row.content, authorBio, null, null, []));
        }

        var older = await GetNeighbourAsync(connection, row, older: true, cancellationToken);
        var newer = await GetNeighbourAsync(connection, row, older: false, cancellationToken);

        var candidates = await PublishedPosts.QueryAsync(
            connection, "p.post_type = 1 AND p.id <> @Id", "", new { Id = row.id }, cancellationToken);
        var candidateCategories = await BlogPostCategoryLinks.GetForPostsAsync(
            connection, candidates.Select(c => c.id).ToList(), cancellationToken);
        var related = BlogSearchRanking.Rank(candidates, candidateCategories, row.title, cancellationToken)
            .Take(RelatedPostCount)
            .Select(r => PublishedPosts.ToSummary(r.Row, candidateCategories[r.Row.id]))
            .ToList();

        return TypedResults.Ok(new PublishedPostResponse(post, row.content, authorBio, older, newer, related));
    }

    private static async Task<PostLink?> GetNeighbourAsync(
        System.Data.IDbConnection connection, PublishedPosts.Row current, bool older, CancellationToken cancellationToken)
    {
        // (published_at, id) tuple comparison keeps the ordering total even if two posts were
        // published at the same instant (e.g. both migrated with the same timestamp).
        var where = older
            ? "p.post_type = 1 AND (p.published_at, p.id) < (@PublishedAt, @Id)"
            : "p.post_type = 1 AND (p.published_at, p.id) > (@PublishedAt, @Id)";
        var order = older
            ? "ORDER BY p.published_at DESC, p.id DESC LIMIT 1"
            : "ORDER BY p.published_at ASC, p.id ASC LIMIT 1";

        var neighbour = (await PublishedPosts.QueryAsync(
            connection, where, order, new { PublishedAt = current.published_at, Id = current.id }, cancellationToken)).FirstOrDefault();

        return neighbour is null ? null : new PostLink(neighbour.title, neighbour.slug);
    }

    public sealed record PostLink(string Title, string Slug);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record PublishedPostResponse(
        PublishedPostSummary Post,
        string Content,
        string? AuthorBio,
        PostLink? Older,
        PostLink? Newer,
        IReadOnlyList<PublishedPostSummary> Related);
}
