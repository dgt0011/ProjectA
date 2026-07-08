using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Categories.CreateCategory;

public static class CreateCategoryEndpoint
{
    public static void MapCreateCategory(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateCategory")
            .WithSummary("Create a category")
            .WithDescription("Creates a new category.");
    }

    private static async Task<Results<CreatedAtRoute<CategoryResponse>, ValidationProblem>> Handle(
        CreateCategoryRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new CategoryDto
        {
            title = request.Title,
            description = request.Description
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.InsertAsync(entity);

        var response = new CategoryResponse(entity.id, entity.title, entity.description);

        return TypedResults.CreatedAtRoute(response, "GetCategoryById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateCategoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateCategoryRequest(string Title, string? Description);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record CategoryResponse(long Id, string Title, string? Description);
}
