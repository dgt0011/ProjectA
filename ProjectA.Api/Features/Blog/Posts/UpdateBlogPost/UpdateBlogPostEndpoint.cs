using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Posts.UpdateBlogPost;

public static class UpdateBlogPostEndpoint
{
    public static void MapUpdateBlogPost(this RouteGroupBuilder group)
    {
        group.MapPut("{id:long}", Handle)
            .WithName("UpdateBlogPost")
            .WithSummary("Update a blog post or page")
            .WithDescription(
                "Replaces every editable field. Publishing a draft stamps PublishedAt with the " +
                "current time; an already-published post keeps its original date; unpublishing " +
                "turns it back into a draft. The author and view count are never changed here.");
    }

    private static async Task<Results<Ok<BlogPostResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        long id,
        UpdateBlogPostRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var entity = await connection.QuerySingleOrDefaultAsync<BlogPostDto>(new CommandDefinition(
            "SELECT * FROM blog_posts WHERE id = @Id;", new { Id = id }, cancellationToken: cancellationToken));
        if (entity is null)
        {
            return BlogPostResponses.NotFound(id);
        }

        // Migrated Blogifier slugs may not satisfy today's stricter slug format - only validate
        // the slug when it is actually being changed, so old posts stay editable as-is.
        var requestedSlug = string.IsNullOrWhiteSpace(request.Slug) ? entity.slug : request.Slug.Trim();
        var slugChanged = requestedSlug != entity.slug;

        var errors = BlogPostValidation.Validate(
            request.Title, slugChanged ? requestedSlug : null, request.Description, request.Cover, request.PostType, request.Categories);
        if (errors.Count == 0 && slugChanged && await BlogSlugs.ExistsAsync(connection, requestedSlug, id, cancellationToken))
        {
            errors["Slug"] = [$"The slug '{requestedSlug}' is already used by another post."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = DateTime.UtcNow;
        entity.post_type = request.PostType;
        entity.title = request.Title.Trim();
        entity.slug = requestedSlug;
        entity.description = request.Description?.Trim() ?? string.Empty;
        entity.content = request.Content ?? string.Empty;
        entity.cover = BlogPostValidation.NullIfBlank(request.Cover);
        entity.is_featured = request.IsFeatured;
        entity.published_at = request.IsPublished ? entity.published_at ?? now : null;
        entity.date_modified = now;

        using var transaction = connection.BeginTransaction();
        await connection.UpdateAsync(entity, transaction);
        await BlogPostCategoryLinks.ReplaceAsync(connection, transaction, id, request.Categories, cancellationToken);
        transaction.Commit();

        return TypedResults.Ok((await BlogPostResponses.LoadAsync(connection, id, cancellationToken))!);
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record UpdateBlogPostRequest(
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
