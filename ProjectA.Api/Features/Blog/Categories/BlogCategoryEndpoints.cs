using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Categories;

// Blog categories are small enough (title + description) that the five CRUD handlers live in
// one slice file; they share the response shape, validation and not-found result.
public static class BlogCategoryEndpoints
{
    public static void MapBlogCategoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("", GetList)
            .WithName("GetBlogCategoryList")
            .WithSummary("List blog categories with how many posts use each");

        group.MapGet("{id:long}", GetById)
            .WithName("GetBlogCategoryById")
            .WithSummary("Get a blog category by Id");

        group.MapPost("", Create)
            .WithName("CreateBlogCategory")
            .WithSummary("Create a blog category");

        group.MapPut("{id:long}", Update)
            .WithName("UpdateBlogCategory")
            .WithSummary("Rename / re-describe a blog category")
            .WithDescription("Renaming changes the public /categories/{title} URL for that category.");

        group.MapDelete("{id:long}", Delete)
            .WithName("DeleteBlogCategory")
            .WithSummary("Delete a blog category")
            .WithDescription("Posts that used it simply lose that category; the posts themselves are untouched.");
    }

    private const string SelectWithCounts =
        """
        SELECT c.id AS Id, c.title AS Title, c.description AS Description,
               (SELECT COUNT(*)::int FROM blog_post_categories pc WHERE pc.category_id = c.id) AS PostCount
        FROM blog_categories c
        """;

    private static async Task<Ok<List<BlogCategoryResponse>>> GetList(
        IDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<BlogCategoryResponse>(new CommandDefinition(
            $"{SelectWithCounts} ORDER BY LOWER(c.title);", cancellationToken: cancellationToken));
        return TypedResults.Ok(items.ToList());
    }

    private static async Task<Results<Ok<BlogCategoryResponse>, ProblemHttpResult>> GetById(
        long id, IDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var item = await LoadAsync(connection, id, cancellationToken);
        return item is null ? NotFound(id) : TypedResults.Ok(item);
    }

    private static async Task<Results<CreatedAtRoute<BlogCategoryResponse>, ValidationProblem>> Create(
        BlogCategoryRequest request, IDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await BlogPostCategoryLinks.LockCategoryWritesAsync(connection, transaction, cancellationToken);
        var errors = await ValidateAsync(connection, transaction, request, null, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new BlogCategoryDto
        {
            title = request.Title.Trim(),
            description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            date_created = DateTime.UtcNow
        };
        await connection.InsertAsync(entity, transaction);
        transaction.Commit();

        var response = (await LoadAsync(connection, entity.id, cancellationToken))!;
        return TypedResults.CreatedAtRoute(response, "GetBlogCategoryById", new { id = entity.id });
    }

    private static async Task<Results<Ok<BlogCategoryResponse>, ValidationProblem, ProblemHttpResult>> Update(
        long id, BlogCategoryRequest request, IDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var entity = await connection.GetAsync<BlogCategoryDto>(id);
        if (entity is null)
        {
            return NotFound(id);
        }

        using var transaction = connection.BeginTransaction();
        await BlogPostCategoryLinks.LockCategoryWritesAsync(connection, transaction, cancellationToken);
        var errors = await ValidateAsync(connection, transaction, request, id, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        entity.title = request.Title.Trim();
        entity.description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.date_modified = DateTime.UtcNow;
        await connection.UpdateAsync(entity, transaction);
        transaction.Commit();

        return TypedResults.Ok((await LoadAsync(connection, id, cancellationToken))!);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(
        long id, IDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM blog_categories WHERE id = @Id;", new { Id = id }, cancellationToken: cancellationToken));
        return deleted > 0 ? TypedResults.NoContent() : NotFound(id);
    }

    private static Task<BlogCategoryResponse?> LoadAsync(System.Data.IDbConnection connection, long id, CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<BlogCategoryResponse>(new CommandDefinition(
            $"{SelectWithCounts} WHERE c.id = @Id;", new { Id = id }, cancellationToken: cancellationToken));

    private static async Task<Dictionary<string, string[]>> ValidateAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        BlogCategoryRequest request,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }
        else if (BlogPostCategoryLinks.ValidateTitle(request.Title.Trim()) is { } titleError)
        {
            errors[nameof(request.Title)] = [titleError];
        }
        else if (await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                     "SELECT EXISTS (SELECT 1 FROM blog_categories WHERE LOWER(title) = LOWER(@Title) AND (@ExcludeId::bigint IS NULL OR id <> @ExcludeId));",
                     new { Title = request.Title.Trim(), ExcludeId = excludeId },
                     transaction,
                     cancellationToken: cancellationToken)))
        {
            // Category titles are the public URL (/categories/{title}), so two that differ only
            // by case would be indistinguishable to readers.
            errors[nameof(request.Title)] = ["A blog category with this title already exists."];
        }

        if (request.Description is { Length: > 255 })
        {
            errors[nameof(request.Description)] = ["Description must be 255 characters or fewer."];
        }

        return errors;
    }

    private static ProblemHttpResult NotFound(long id) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Blog category not found",
            detail: $"No blog category exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

    // Request/response shapes - owned by this slice, not shared.
    public sealed record BlogCategoryRequest(string Title, string? Description);

    public sealed record BlogCategoryResponse(long Id, string Title, string? Description, int PostCount);
}
