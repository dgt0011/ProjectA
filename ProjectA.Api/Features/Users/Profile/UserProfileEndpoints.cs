using System.Security.Claims;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Users.Profile;

// Self-service, like ChangePassword: always the caller's own account (from the token's "uid"
// claim). These are the author details the public blog shows on posts (Blogifier kept them on
// its separate Authors table).
public static class UserProfileEndpoints
{
    public static void MapUserProfile(this RouteGroupBuilder group)
    {
        group.MapGet("profile", Get)
            .WithName("GetUserProfile")
            .WithSummary("Get the signed-in caller's blog author profile");

        group.MapPut("profile", Update)
            .WithName("UpdateUserProfile")
            .WithSummary("Update the signed-in caller's blog author profile")
            .WithDescription("DisplayName/Bio/Avatar appear publicly on the blog for posts this user authors.");
    }

    private static async Task<Results<Ok<UserProfileResponse>, ProblemHttpResult>> Get(
        ClaimsPrincipal caller, IDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var entity = await LoadCallerAsync(connection, caller);
        return entity is null ? NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async Task<Results<Ok<UserProfileResponse>, ValidationProblem, ProblemHttpResult>> Update(
        UpdateUserProfileRequest request,
        ClaimsPrincipal caller,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.DisplayName is { Length: > 160 })
        {
            errors[nameof(request.DisplayName)] = ["Display name must be 160 characters or fewer."];
        }

        if (request.Bio is { Length: > 2000 })
        {
            errors[nameof(request.Bio)] = ["Bio must be 2000 characters or fewer."];
        }

        if (request.Avatar is { Length: > 400 })
        {
            errors[nameof(request.Avatar)] = ["Avatar must be 400 characters or fewer."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var entity = await LoadCallerAsync(connection, caller);
        if (entity is null)
        {
            return NotFound();
        }

        entity.display_name = Blank(request.DisplayName);
        entity.bio = Blank(request.Bio);
        entity.avatar = Blank(request.Avatar);
        entity.date_modified = DateTime.UtcNow;

        // Only the profile columns: a whole-row update would write back the password_hash and
        // username read above, silently undoing a password change made in the meantime.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE users SET display_name = @DisplayName, bio = @Bio, avatar = @Avatar, date_modified = @DateModified
            WHERE id = @Id;
            """,
            new
            {
                Id = entity.id,
                DisplayName = entity.display_name,
                Bio = entity.bio,
                Avatar = entity.avatar,
                DateModified = entity.date_modified
            },
            cancellationToken: cancellationToken));

        return TypedResults.Ok(ToResponse(entity));
    }

    private static async Task<UserDto?> LoadCallerAsync(System.Data.IDbConnection connection, ClaimsPrincipal caller) =>
        long.TryParse(caller.FindFirst("uid")?.Value, out var id) ? await connection.GetAsync<UserDto>(id) : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UserProfileResponse ToResponse(UserDto e) => new(e.id, e.username, e.display_name, e.bio, e.avatar);

    private static ProblemHttpResult NotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "User not found",
            detail: "The signed-in user's account no longer exists.",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");

    // Request/response shapes - owned by this slice, not shared.
    public sealed record UpdateUserProfileRequest(string? DisplayName, string? Bio, string? Avatar);

    public sealed record UserProfileResponse(long Id, string Username, string? DisplayName, string? Bio, string? Avatar);
}
