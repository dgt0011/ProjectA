using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using ProjectA.Api.Data;
using ProjectA.Api.Security;

namespace ProjectA.Api.Features.Users.CreateUser;

public static class CreateUserEndpoint
{
    public static void MapCreateUser(this RouteGroupBuilder group)
    {
        group.MapPost("", Handle)
            .WithName("CreateUser")
            .WithSummary("Create a user")
            .WithDescription(
                "Creates a new user account. There is no public self-registration endpoint - " +
                "this requires an already-authenticated caller, which is how new accounts get " +
                "provisioned.");
    }

    private static async Task<Results<CreatedAtRoute<UserResponse>, ValidationProblem>> Handle(
        CreateUserRequest request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new UserDto
        {
            username = request.Username,
            password_hash = PasswordHasher.Hash(request.Password),
            date_created = DateTime.UtcNow
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        try
        {
            await connection.InsertAsync(entity);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Username)] = ["Username is already taken."]
            });
        }

        var response = new UserResponse(entity.id, entity.username, entity.date_created, entity.date_modified);

        return TypedResults.CreatedAtRoute(response, "GetUserById", new { id = response.Id });
    }

    private static Dictionary<string, string[]> Validate(CreateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors[nameof(request.Username)] = ["Username is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            errors[nameof(request.Password)] = ["Password is required and must be at least 8 characters."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record CreateUserRequest(string Username, string Password);

    // Shape returned to callers of this endpoint - owned by this slice, not shared. Never
    // includes password_hash.
    public sealed record UserResponse(long Id, string Username, DateTime DateCreated, DateTime? DateModified);
}
