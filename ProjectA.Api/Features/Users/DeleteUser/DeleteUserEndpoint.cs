using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Users.DeleteUser;

public static class DeleteUserEndpoint
{
    public static void MapDeleteUser(this RouteGroupBuilder group)
    {
        group.MapDelete("{id}", Handle)
            .WithName("DeleteUser")
            .WithSummary("Delete a user")
            .WithDescription(
                "Permanently removes a user by Id. Refuses to delete the last remaining user, " +
                "since there would be no way to log back in and provision another account " +
                "afterwards.");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        uint id,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var userCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition("SELECT COUNT(*) FROM users;", cancellationToken: cancellationToken));

        if (userCount <= 1)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Cannot delete the last user",
                detail: "At least one user account must remain so someone can still log in.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.8");
        }

        var deleted = await connection.DeleteAsync(new UserDto { id = (long)id });

        if (!deleted)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: $"No user exists with id {id}.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return TypedResults.NoContent();
    }
}
