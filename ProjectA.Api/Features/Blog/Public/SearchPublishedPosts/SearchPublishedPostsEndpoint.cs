using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Public.SearchPublishedPosts;

public static class SearchPublishedPostsEndpoint
{
    public static void MapSearchPublishedPosts(this RouteGroupBuilder group)
    {
        group.MapGet("search", Handle)
            .WithName("SearchPublishedPosts")
            .WithSummary("Search published blog posts")
            .WithDescription(
                "Ranks published posts by how often the search terms appear in their title, " +
                "description, content and categories (Blogifier's ranking), best match first.");
    }

    private static async Task<Ok<PublishedPostPage>> Handle(
        IDbConnectionFactory connectionFactory,
        string? term,
        int? page,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = PublishedPosts.NormalizePage(page);

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var itemsPerPage = await PublishedPosts.GetItemsPerPageAsync(connection, cancellationToken);

        if (string.IsNullOrWhiteSpace(term))
        {
            return TypedResults.Ok(PublishedPostPage.Create([], pageNumber, itemsPerPage, 0));
        }

        // Ranking needs every candidate's full text, so this loads all published posts and
        // ranks in memory - fine at personal-blog scale (tens to low hundreds of posts).
        var rows = await PublishedPosts.QueryAsync(connection, "p.post_type = 1", "", null, cancellationToken);
        var categories = await BlogPostCategoryLinks.GetForPostsAsync(connection, rows.Select(r => r.id).ToList(), cancellationToken);

        var ranked = BlogSearchRanking.Rank(rows, categories, term, cancellationToken);
        var items = ranked
            .Skip((pageNumber - 1) * itemsPerPage)
            .Take(itemsPerPage)
            .Select(r => PublishedPosts.ToSummary(r.Row, categories[r.Row.id]))
            .ToList();

        return TypedResults.Ok(PublishedPostPage.Create(items, pageNumber, itemsPerPage, ranked.Count));
    }
}
