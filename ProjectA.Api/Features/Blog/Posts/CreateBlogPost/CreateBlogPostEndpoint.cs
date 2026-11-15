using System.Security.Claims;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Posts.CreateBlogPost;

public static class CreateBlogPostEndpoint
{
    public static void MapCreateBlogPost(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateBlogPost")
            .WithSummary("Create a blog post or page")
            .WithDescription(
                "The signed-in user becomes the author. A blank Slug is generated from the Title " +
                "(made unique by appending 2, 3, ...); an explicit Slug that's already taken is " +
                "rejected. Categories are titles - unknown ones are created.");
    }

    private static async Task<Results<CreatedAtRoute<BlogPostResponse>, ValidationProblem>> Handle(
        CreateBlogPostRequest request,
        ClaimsPrincipal caller,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = BlogPostValidation.Validate(
            request.Title, request.Slug, request.Description, request.Cover, request.PostType, request.Categories);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        string slug;
        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = await BlogSlugs.MakeUniqueAsync(connection, BlogSlugs.FromTitle(request.Title), null, cancellationToken);
        }
        else
        {
            slug = request.Slug.Trim();
            if (await BlogSlugs.ExistsAsync(connection, slug, null, cancellationToken))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Slug"] = [$"The slug '{slug}' is already used by another post."]
                });
            }
        }

        var now = DateTime.UtcNow;
        var entity = new BlogPostDto
        {
            author_id = BlogCaller.GetUserId(caller),
            post_type = request.PostType,
            title = request.Title.Trim(),
            slug = slug,
            description = request.Description?.Trim() ?? string.Empty,
            content = request.Content ?? string.Empty,
            cover = BlogPostValidation.NullIfBlank(request.Cover),
            is_featured = request.IsFeatured,
            published_at = request.IsPublished ? now : null,
            date_created = now
        };

        using var transaction = connection.BeginTransaction();
        // Dapper.Contrib writes the generated identity back onto entity.id (its int return
        // value would truncate a BIGINT id, so it's deliberately not used).
        await connection.InsertAsync(entity, transaction);
        var id = entity.id;
        await BlogPostCategoryLinks.ReplaceAsync(connection, transaction, id, request.Categories, cancellationToken);
        transaction.Commit();

        var response = (await BlogPostResponses.LoadAsync(connection, id, cancellationToken))!;
        return TypedResults.CreatedAtRoute(response, "GetBlogPostById", new { id });
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateBlogPostRequest(
        string Title,
        string? Slug,
        string? Description,
        string? Content,
        string? Cover,
        short PostType = BlogPostTypes.Post,
        bool IsFeatured = false,
        bool IsPublished = false,
        IReadOnlyList<string>? Categories = null);
}
