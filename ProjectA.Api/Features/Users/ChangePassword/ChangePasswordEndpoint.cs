using System.Security.Claims;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;
using ProjectA.Api.Security;

namespace ProjectA.Api.Features.Users.ChangePassword;

public static class ChangePasswordEndpoint
{
    public static void MapChangePassword(this RouteGroupBuilder group)
    {
        group.MapPost("change-password", Handle)
            .WithName("ChangePassword")
            .WithSummary("Change the signed-in caller's own password")
            .WithDescription(
                "Self-service password change. The account changed is always whoever the " +
                "caller's own bearer token identifies (the 'uid' claim) - there is no way to " +
                "pass a different user's Id, even accidentally, since none is accepted in the " +
                "request body. The caller's CurrentPassword must verify against the stored hash " +
                "before NewPassword is accepted.");
    }

    private static async Task<Results<Ok<ChangePasswordResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        ChangePasswordRequest request,
        ClaimsPrincipal caller,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // This group already has .RequireAuthorization(), so a valid "uid" claim is always
        // present here - this is purely a defensive fallback, never expected to trigger.
        var uidClaim = caller.FindFirst("uid")?.Value;
        if (!long.TryParse(uidClaim, out var callerId))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Could not identify the signed-in user",
                detail: "The access token did not contain a valid user id.",
                type: "https://tools.ietf.org/html/rfc7235#section-3.1");
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var entity = await connection.GetAsync<UserDto>(callerId);

        if (entity is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: "The signed-in user's account no longer exists.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        if (!PasswordHasher.Verify(request.CurrentPassword, entity.password_hash))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.CurrentPassword)] = ["Current password is incorrect."]
            });
        }

        entity.password_hash = PasswordHasher.Hash(request.NewPassword);
        entity.date_modified = DateTime.UtcNow;

        await connection.UpdateAsync(entity);

        var response = new ChangePasswordResponse(entity.id, entity.username, entity.date_modified);
        return TypedResults.Ok(response);
    }

    private static Dictionary<string, string[]> Validate(ChangePasswordRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            errors[nameof(request.CurrentPassword)] = ["Current password is required."];
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            errors[nameof(request.NewPassword)] = ["New password is required and must be at least 8 characters."];
        }

        return errors;
    }

    // Request body accepted by this endpoint - owned by this slice, not shared. Deliberately
    // has no Id/Username field - the account changed is always the caller's own, resolved
    // server-side from the bearer token, never from anything the client sends.
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    // Shape returned to callers of this endpoint - owned by this slice, not shared. Never
    // includes password_hash.
    public sealed record ChangePasswordResponse(long Id, string Username, DateTime? DateModified);
}
