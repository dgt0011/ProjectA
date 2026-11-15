using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Public.GetBlogSite;

public static class GetBlogSiteEndpoint
{
    public static void MapGetBlogSite(this RouteGroupBuilder group)
    {
        group.MapGet("site", Handle)
            .WithName("GetBlogSite")
            .WithSummary("Get the public blog's site-wide data")
            .WithDescription(
                "Returns everything the public blog layout needs on every page: title, description, " +
                "header/footer scripts and the categories that have at least one published post.");
    }

    private static async Task<Ok<BlogSiteResponse>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var settings = await connection.QueryFirstOrDefaultAsync<BlogSettingsDto>(new CommandDefinition(
            "SELECT * FROM blog_settings ORDER BY id LIMIT 1;",
            cancellationToken: cancellationToken)) ?? new BlogSettingsDto { title = "Blog" };

        var categories = await connection.QueryAsync<BlogSiteCategory>(new CommandDefinition(
            $"""
            SELECT c.title AS Title, COUNT(*)::int AS PostCount
            FROM blog_categories c
            JOIN blog_post_categories pc ON pc.category_id = c.id
            JOIN blog_posts p ON p.id = pc.post_id
            WHERE {PublishedPosts.VisibleFilter}
            GROUP BY c.title
            ORDER BY LOWER(c.title);
            """,
            cancellationToken: cancellationToken));

        return TypedResults.Ok(new BlogSiteResponse(
            settings.title,
            settings.description,
            settings.items_per_page,
            settings.include_featured,
            settings.cover,
            settings.logo,
            settings.header_script,
            settings.footer_script,
            categories.ToList()));
    }

    public sealed record BlogSiteCategory(string Title, int PostCount);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record BlogSiteResponse(
        string Title,
        string? Description,
        int ItemsPerPage,
        bool IncludeFeatured,
        string? Cover,
        string? Logo,
        string? HeaderScript,
        string? FooterScript,
        IReadOnlyList<BlogSiteCategory> Categories);
}
