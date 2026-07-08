using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Categories.GetCategoryList;

public static class GetCategoryListEndpoint
{
    public static void MapGetCategoryList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetCategoryList")
            .WithSummary("List categories")
            .WithDescription("Returns all categories.");
    }

    private static async Task<Ok<List<CategoryListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<CategoryDto>();

            var items = entities
                .Select(entity => new CategoryListItemResponse(entity.id, entity.title, entity.description))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<CategoryListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record CategoryListItemResponse(long Id, string Title, string? Description);
}
