using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Public.GetPublishedPostList;

public static class GetPublishedPostListEndpoint
{
    public static void MapGetPublishedPostList(this RouteGroupBuilder group)
    {
        group.MapGet("published", Handle)
            .WithName("GetPublishedPostList")
            .WithSummary("List published blog posts, a page at a time")
            .WithDescription(
                "Newest first. With no category, featured posts are only included when the blog's " +
                "IncludeFeatured setting is on (the same rule Blogifier applied to its home page). " +
                "With a category (matched case-insensitively) every published post in it is listed. " +
                "Pages (post type 2) are never listed.");
    }

    private static async Task<Ok<PublishedPostPage>> Handle(
        IDbConnectionFactory connectionFactory,
        int? page,
        string? category,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = PublishedPosts.NormalizePage(page);

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var itemsPerPage = await PublishedPosts.GetItemsPerPageAsync(connection, cancellationToken);
        var includeFeatured = await connection.ExecuteScalarAsync<bool?>(new CommandDefinition(
            "SELECT include_featured FROM blog_settings ORDER BY id LIMIT 1;",
            cancellationToken: cancellationToken)) ?? true;

        string where;
        object parameters;
        if (!string.IsNullOrWhiteSpace(category))
        {
            where = """
                    p.post_type = 1 AND EXISTS (
                        SELECT 1 FROM blog_post_categories pc
                        JOIN blog_categories c ON c.id = pc.category_id
                        WHERE pc.post_id = p.id AND LOWER(c.title) = LOWER(@Category))
                    """;
            parameters = new { Category = category.Trim(), Limit = itemsPerPage, Offset = (pageNumber - 1) * itemsPerPage };
        }
        else
        {
            where = includeFeatured ? "p.post_type = 1" : "p.post_type = 1 AND NOT p.is_featured";
            parameters = new { Limit = itemsPerPage, Offset = (pageNumber - 1) * itemsPerPage };
        }

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*)::int FROM blog_posts p WHERE {PublishedPosts.VisibleFilter} AND ({where});",
            parameters,
            cancellationToken: cancellationToken));

        var rows = await PublishedPosts.QueryAsync(
            connection, where, "ORDER BY p.published_at DESC, p.id DESC LIMIT @Limit OFFSET @Offset", parameters, cancellationToken);

        var items = await PublishedPosts.ToSummariesAsync(connection, rows, cancellationToken);

        return TypedResults.Ok(PublishedPostPage.Create(items, pageNumber, itemsPerPage, total));
    }
}
