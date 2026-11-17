using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Posts.GetBlogPostList;

public static class GetBlogPostListEndpoint
{
    public static void MapGetBlogPostList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetBlogPostList")
            .WithSummary("List blog posts (admin)")
            .WithDescription(
                "Lists posts of one type (1 = post, 2 = page; default 1) including drafts. status " +
                "narrows to 'published', 'drafts' or 'featured' (default: all); term filters on title.");
    }

    private static async Task<Results<Ok<List<BlogPostListItemResponse>>, ValidationProblem>> Handle(
        IDbConnectionFactory connectionFactory,
        string? status,
        short? type,
        string? term,
        CancellationToken cancellationToken = default)
    {
        var postType = type ?? BlogPostTypes.Post;
        if (!BlogPostTypes.IsValid(postType))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Type"] = ["Type must be 1 (post) or 2 (page)."]
            });
        }

        var statusFilter = (status ?? "all").ToLowerInvariant() switch
        {
            "published" => "AND p.published_at IS NOT NULL",
            "drafts" => "AND p.published_at IS NULL",
            "featured" => "AND p.is_featured",
            "all" => "",
            _ => null
        };

        if (statusFilter is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Status"] = ["Status must be one of: all, published, drafts, featured."]
            });
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<BlogPostDto>(new CommandDefinition(
            $"""
            SELECT p.* FROM blog_posts p
            WHERE p.post_type = @PostType {statusFilter}
              AND (@Term::text IS NULL OR p.title ILIKE '%' || @Term || '%')
            ORDER BY p.published_at IS NOT NULL, p.published_at DESC, p.id DESC;
            """,
            new { PostType = postType, Term = string.IsNullOrWhiteSpace(term) ? null : term.Trim() },
            cancellationToken: cancellationToken))).ToList();

        var categories = await BlogPostCategoryLinks.GetForPostsAsync(connection, rows.Select(r => r.id).ToList(), cancellationToken);

        var items = rows
            .Select(p => new BlogPostListItemResponse(
                p.id, p.post_type, p.title, p.slug, p.is_featured, p.published_at, p.post_views,
                categories[p.id].Select(c => c.Title).ToList(), p.date_created, p.date_modified))
            .ToList();

        return TypedResults.Ok(items);
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BlogPostListItemResponse(
        long Id,
        short PostType,
        string Title,
        string Slug,
        bool IsFeatured,
        DateTime? PublishedAt,
        int PostViews,
        IReadOnlyList<string> Categories,
        DateTime DateCreated,
        DateTime? DateModified);
}
