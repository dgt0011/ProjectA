using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Users.GetUserList;

public static class GetUserListEndpoint
{
    public static void MapGetUserList(this RouteGroupBuilder group)
    {
        group.MapGet("", Handle)
            .WithName("GetUserList")
            .WithSummary("List users")
            .WithDescription("Returns all users. Never includes password hashes.");
    }

    private static async Task<Ok<List<UserListItemResponse>>> Handle(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entities = await connection.GetAllAsync<UserDto>();

            var items = entities
                .OrderBy(entity => entity.username, StringComparer.OrdinalIgnoreCase)
                .Select(entity => new UserListItemResponse(entity.id, entity.username, entity.date_created, entity.date_modified))
                .ToList();

            return TypedResults.Ok(items);
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
            return TypedResults.Ok(new List<UserListItemResponse>());
        }
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record UserListItemResponse(long Id, string Username, DateTime DateCreated, DateTime? DateModified);
}
