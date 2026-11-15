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

        // Only the editable columns are written. post_views in particular is bumped concurrently
        // by readers, so writing back the value read above would lose any views recorded since.
        // published_at: keep the stored date if already published (COALESCE against the row as
        // it is now, not as read), stamp now when publishing a draft, clear when unpublishing.
        try
        {
            using var transaction = connection.BeginTransaction();
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE blog_posts SET
                    post_type = @PostType,
                    title = @Title,
                    slug = @Slug,
                    description = @Description,
                    content = @Content,
                    cover = @Cover,
                    is_featured = @IsFeatured,
                    published_at = CASE WHEN @IsPublished THEN COALESCE(published_at, @Now) ELSE NULL END,
                    date_modified = @Now
                WHERE id = @Id;
                """,
                new
                {
                    Id = id,
                    PostType = request.PostType,
                    Title = request.Title.Trim(),
                    Slug = requestedSlug,
                    Description = request.Description?.Trim() ?? string.Empty,
                    Content = request.Content ?? string.Empty,
                    Cover = BlogPostValidation.NullIfBlank(request.Cover),
                    request.IsFeatured,
                    request.IsPublished,
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
            if (updated == 0)
            {
                // Deleted since it was read above.
                return BlogPostResponses.NotFound(id);
            }

            await BlogPostCategoryLinks.ReplaceAsync(connection, transaction, id, request.Categories, cancellationToken);
            transaction.Commit();
        }
        catch (Npgsql.PostgresException ex) when (BlogSlugs.IsSlugConflict(ex))
        {
            return BlogSlugs.ConflictProblem(requestedSlug);
        }

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
