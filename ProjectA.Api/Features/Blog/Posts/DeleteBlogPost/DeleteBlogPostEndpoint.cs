using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Posts.DeleteBlogPost;

public static class DeleteBlogPostEndpoint
{
    public static void MapDeleteBlogPost(this RouteGroupBuilder group)
    {
        group.MapDelete("{id:long}", Handle)
            .WithName("DeleteBlogPost")
            .WithSummary("Delete a blog post or page")
            .WithDescription("Its category links are removed with it (ON DELETE CASCADE); the categories themselves remain.");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        long id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM blog_posts WHERE id = @Id;", new { Id = id }, cancellationToken: cancellationToken));

        return deleted > 0 ? TypedResults.NoContent() : BlogPostResponses.NotFound(id);
    }
}
