using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Users;
using ProjectA.Api.Security;

namespace ProjectA.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost("login", Handle)
            .WithName("Login")
            .WithSummary("Log in")
            .WithDescription(
                "Exchanges a username and password for a bearer token. This endpoint itself " +
                "is intentionally not behind [Authorize] - it's how a caller gets a token in " +
                "the first place.");
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult, ValidationProblem>> Handle(
        LoginRequest request,
        IDbConnectionFactory connectionFactory,
        JwtTokenService tokenService,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        // Case-insensitive lookup, matching the unique index on LOWER(username) - this isn't
        // a primary-key lookup, so it goes through plain Dapper rather than Dapper.Contrib's
        // GetAsync (which only looks up by Id).
        var user = await connection.QuerySingleOrDefaultAsync<UserDto>(
            new CommandDefinition(
                "SELECT * FROM users WHERE LOWER(username) = LOWER(@Username);",
                new { request.Username },
                cancellationToken: cancellationToken));

        if (user is null || !PasswordHasher.Verify(request.Password, user.password_hash))
        {
            return Unauthorized();
        }

        var (token, expiresAtUtc) = tokenService.CreateToken(user.id, user.username);

        return TypedResults.Ok(new LoginResponse(token, expiresAtUtc, user.username));
    }

    private static Results<Ok<LoginResponse>, ProblemHttpResult, ValidationProblem> Unauthorized() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Invalid credentials",
            detail: "The username or password is incorrect.",
            type: "https://tools.ietf.org/html/rfc7235#section-3.1");

    private static Dictionary<string, string[]> Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors[nameof(request.Username)] = ["Username is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors[nameof(request.Password)] = ["Password is required."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared.
    public sealed record LoginRequest(string Username, string Password);

    // Shape returned to callers of this endpoint - owned by this slice, not shared.
    public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, string Username);
}
