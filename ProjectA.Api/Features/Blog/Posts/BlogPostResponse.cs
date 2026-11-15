using System.Data;
using Dapper;

namespace ProjectA.Api.Features.Blog.Posts;

// The full editable shape of one post, returned by get-by-id, create and update alike (as
// CategoryResponse is for categories) so the admin editor round-trips a single shape.
public sealed record BlogPostResponse(
    long Id,
    short PostType,
    string Title,
    string Slug,
    string Description,
    string Content,
    string? Cover,
    bool IsFeatured,
    DateTime? PublishedAt,
    int PostViews,
    long? AuthorId,
    string? AuthorName,
    IReadOnlyList<string> Categories,
    DateTime DateCreated,
    DateTime? DateModified);

internal static class BlogPostResponses
{
    public static async Task<BlogPostResponse?> LoadAsync(IDbConnection connection, long id, CancellationToken cancellationToken)
    {
        var post = await connection.QuerySingleOrDefaultAsync<BlogPostDto>(new CommandDefinition(
            "SELECT * FROM blog_posts WHERE id = @Id;", new { Id = id }, cancellationToken: cancellationToken));
        if (post is null)
        {
            return null;
        }

        var authorName = post.author_id is null
            ? null
            : await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT COALESCE(NULLIF(display_name, ''), username) FROM users WHERE id = @Id;",
                new { Id = post.author_id },
                cancellationToken: cancellationToken));

        var categories = await BlogPostCategoryLinks.GetForPostsAsync(connection, [post.id], cancellationToken);

        return new BlogPostResponse(
            post.id,
            post.post_type,
            post.title,
            post.slug,
            post.description,
            post.content,
            post.cover,
            post.is_featured,
            post.published_at,
            post.post_views,
            post.author_id,
            authorName,
            categories[post.id].Select(c => c.Title).ToList(),
            post.date_created,
            post.date_modified);
    }

    public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult NotFound(long id) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Blog post not found",
            detail: $"No blog post exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
}
