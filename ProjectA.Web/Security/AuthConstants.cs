namespace ProjectA.Web.Security;

public static class AuthConstants
{
    // Holds the bearer token issued by ProjectA.Api's /api/auth/login, stashed on the browser's
    // cookie identity at sign-in (see Pages/Account/Login.cshtml.cs) so BearerTokenHandler can
    // forward it on every outgoing API call without a separate server-side token store.
    public const string ApiTokenClaimType = "api_token";
}
