using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Categories.UpdateCategory;

public static class UpdateCategoryEndpoint
{
    public static void MapUpdateCategory(this RouteGroupBuilder group)
    {
        group.MapPut("{id}", Handle)
            .WithName("UpdateCategory")
            .WithSummary("Update a category")
            .WithDescription("Replaces an existing category's title and description.");
    }

    private static async Task<Results<Ok<CategoryResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        uint id,
        UpdateCategoryRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Unlike ToDo, a category has no fields the request doesn't already carry (no
        // date_created/date_modified to preserve), so this can build the entity directly
        // and rely on UpdateAsync's affected-row count for the not-found case, rather than
        // reading the row first.
        var entity = new CategoryDto
        {
            id = (long)id,
            title = request.Title,
            description = request.Description
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var updated = await connection.UpdateAsync(entity);

        if (!updated)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Category not found",
                detail: $"No category exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.Ok(new CategoryResponse(entity.id, entity.title, entity.description));
    }

    private static Dictionary<string, string[]> Validate(UpdateCategoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record UpdateCategoryRequest(string Title, string? Description);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record CategoryResponse(long Id, string Title, string? Description);
}
