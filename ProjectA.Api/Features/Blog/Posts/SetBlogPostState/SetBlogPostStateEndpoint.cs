using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Posts.SetBlogPostState;

// The admin post list's one-click actions (Blogifier's GroupAction: Publish/Unpublish/Feature)
// - small targeted updates so toggling a flag doesn't require round-tripping the whole post.
public static class SetBlogPostStateEndpoint
{
    public static void MapSetBlogPostState(this RouteGroupBuilder group)
    {
        group.MapPut("{id:long}/published", HandlePublished)
            .WithName("SetBlogPostPublished")
            .WithSummary("Publish or unpublish a blog post")
            .WithDescription("Publishing keeps an existing PublishedAt, or stamps now() for a draft.");

        group.MapPut("{id:long}/featured", HandleFeatured)
            .WithName("SetBlogPostFeatured")
            .WithSummary("Feature or un-feature a blog post");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandlePublished(
        long id,
        SetFlagRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE blog_posts
            SET published_at = CASE WHEN @Value THEN COALESCE(published_at, now()) ELSE NULL END,
                date_modified = now()
            WHERE id = @Id;
            """,
            new { Id = id, request.Value },
            cancellationToken: cancellationToken));

        return updated > 0 ? TypedResults.NoContent() : BlogPostResponses.NotFound(id);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleFeatured(
        long id,
        SetFlagRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE blog_posts SET is_featured = @Value, date_modified = now() WHERE id = @Id;",
            new { Id = id, request.Value },
            cancellationToken: cancellationToken));

        return updated > 0 ? TypedResults.NoContent() : BlogPostResponses.NotFound(id);
    }

    // Request body accepted by these endpoints - owned by this slice, not shared.
    public sealed record SetFlagRequest(bool Value);
}
