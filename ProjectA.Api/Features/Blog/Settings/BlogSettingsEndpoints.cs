using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Settings;

public static class BlogSettingsEndpoints
{
    public static void MapBlogSettingsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("", Get)
            .WithName("GetBlogSettings")
            .WithSummary("Get the blog's site-wide settings");

        group.MapPut("", Update)
            .WithName("UpdateBlogSettings")
            .WithSummary("Update the blog's site-wide settings")
            .WithDescription(
                "HeaderScript/FooterScript are injected verbatim into every public page (e.g. " +
                "analytics snippets) - only signed-in admins can set them.");
    }

    private static async Task<Ok<BlogSettingsResponse>> Get(
        IDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(await LoadOrCreateAsync(connection, cancellationToken)));
    }

    private static async Task<Results<Ok<BlogSettingsResponse>, ValidationProblem>> Update(
        UpdateBlogSettingsRequest request, IDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var entity = await LoadOrCreateAsync(connection, cancellationToken);

        entity.title = request.Title.Trim();
        entity.description = Blank(request.Description);
        entity.items_per_page = request.ItemsPerPage;
        entity.include_featured = request.IncludeFeatured;
        entity.cover = Blank(request.Cover);
        entity.logo = Blank(request.Logo);
        entity.header_script = Blank(request.HeaderScript);
        entity.footer_script = Blank(request.FooterScript);
        entity.date_modified = DateTime.UtcNow;
        await connection.UpdateAsync(entity);

        return TypedResults.Ok(ToResponse(entity));
    }

    // script014 seeds the row, but recreate it if someone has deleted it by hand rather than
    // failing every blog page.
    private static async Task<BlogSettingsDto> LoadOrCreateAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        var entity = await connection.QueryFirstOrDefaultAsync<BlogSettingsDto>(new CommandDefinition(
            "SELECT * FROM blog_settings ORDER BY id LIMIT 1;", cancellationToken: cancellationToken));
        if (entity is not null)
        {
            return entity;
        }

        entity = new BlogSettingsDto { title = "Blog Title", date_created = DateTime.UtcNow };
        await connection.InsertAsync(entity);
        return entity;
    }

    private static Dictionary<string, string[]> Validate(UpdateBlogSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }
        else if (request.Title.Trim().Length > 160)
        {
            errors[nameof(request.Title)] = ["Title must be 160 characters or fewer."];
        }

        if (request.Description is { Length: > 450 })
        {
            errors[nameof(request.Description)] = ["Description must be 450 characters or fewer."];
        }

        if (request.ItemsPerPage is < 1 or > 100)
        {
            errors[nameof(request.ItemsPerPage)] = ["Items per page must be between 1 and 100."];
        }

        if (request.Cover is { Length: > 400 })
        {
            errors[nameof(request.Cover)] = ["Cover must be 400 characters or fewer."];
        }

        if (request.Logo is { Length: > 400 })
        {
            errors[nameof(request.Logo)] = ["Logo must be 400 characters or fewer."];
        }

        return errors;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BlogSettingsResponse ToResponse(BlogSettingsDto e) =>
        new(e.title, e.description, e.items_per_page, e.include_featured, e.cover, e.logo, e.header_script, e.footer_script);

    // Request/response shapes - owned by this slice, not shared.
    public sealed record UpdateBlogSettingsRequest(
        string Title,
        string? Description,
        int ItemsPerPage,
        bool IncludeFeatured,
        string? Cover,
        string? Logo,
        string? HeaderScript,
        string? FooterScript);

    public sealed record BlogSettingsResponse(
        string Title,
        string? Description,
        int ItemsPerPage,
        bool IncludeFeatured,
        string? Cover,
        string? Logo,
        string? HeaderScript,
        string? FooterScript);
}
