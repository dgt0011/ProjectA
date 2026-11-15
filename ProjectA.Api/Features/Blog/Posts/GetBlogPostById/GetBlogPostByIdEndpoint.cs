using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Posts.GetBlogPostById;

public static class GetBlogPostByIdEndpoint
{
    public static void MapGetBlogPostById(this RouteGroupBuilder group)
    {
        group.MapGet("{id:long}", Handle)
            .WithName("GetBlogPostById")
            .WithSummary("Get a blog post by Id (admin)")
            .WithDescription("Returns a single post or page, drafts included, with its raw Markdown content.");
    }

    private static async Task<Results<Ok<BlogPostResponse>, ProblemHttpResult>> Handle(
        long id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var response = await BlogPostResponses.LoadAsync(connection, id, cancellationToken);

        return response is null ? BlogPostResponses.NotFound(id) : TypedResults.Ok(response);
    }
}
