using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Public.RecordPostView;

public static class RecordPostViewEndpoint
{
    public static void MapRecordPostView(this RouteGroupBuilder group)
    {
        group.MapPost("published/{slug}/views", Handle)
            .WithName("RecordPostView")
            .WithSummary("Count one view of a published post")
            .WithDescription(
                "Called by the public blog each time a post is displayed - feeds the view counts on " +
                "the admin dashboard. Anonymous by design (readers aren't signed in); only ever " +
                "increments, and only for published posts.");
    }

    private static async Task<Results<NoContent, NotFound>> Handle(
        string slug,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE blog_posts p SET post_views = p.post_views + 1 WHERE p.slug = @Slug AND {PublishedPosts.VisibleFilter};",
            new { Slug = slug },
            cancellationToken: cancellationToken));

        return updated > 0 ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
