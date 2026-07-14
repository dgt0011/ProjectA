using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Users.GetUserById;

public static class GetUserByIdEndpoint
{
    public static void MapGetUserById(this RouteGroupBuilder group)
    {
        group.MapGet("{id}", Handle)
            .WithName("GetUserById")
            .WithSummary("Get a user by Id")
            .WithDescription("Returns a single user by Id. Never includes the password hash.");
    }

    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            var entity = await connection.GetAsync<UserDto>((long)id);

            if (entity is not null)
            {
                var response = new UserResponse(entity.id, entity.username, entity.date_created, entity.date_modified);
                return TypedResults.Ok(response);
            }
        }
        catch (Exception)
        {
            // TODO: Some logging is necessary
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "User not found",
            detail: $"No user exists with id {id}.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record UserResponse(long Id, string Username, DateTime DateCreated, DateTime? DateModified);
}
