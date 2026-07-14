using ProjectA.Api.Features.Auth.Login;

namespace ProjectA.Api.Features.Auth;

public static class AuthEndpointsModule
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Deliberately no .RequireAuthorization() anywhere in this group - these are the
        // endpoints an unauthenticated caller uses to become authenticated.
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapLogin();
    }
}
