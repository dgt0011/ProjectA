using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Categories.GetCategoryById;

public static class GetCategoryByIdEndpoint
{
    public static void MapGetCategoryById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetCategoryById")
            .WithSummary("Get a category by Id")
            .WithDescription("Returns a single category by Id.");
    }

    private static async Task<Results<Ok<CategoryResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<CategoryDto>((long)id);

            if (entity is not null)
            {
                return TypedResults.Ok(new CategoryResponse(entity.id, entity.title, entity.description));
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Category not found",
            detail: $"No category exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record CategoryResponse(long Id, string Title, string? Description);
}
