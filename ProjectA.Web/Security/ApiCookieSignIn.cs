using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ProjectA.Web.Models;

namespace ProjectA.Web.Security;

// Used by /Account/Login: turns a successful POST /api/auth/login result into the auth cookie.
// The API token itself lives only as a claim on this cookie's encrypted identity - there is no
// separate server-side session store. BearerTokenHandler reads it back out of HttpContext.User on every outgoing API call.
public static class ApiCookieSignIn
{
    public static Task SignInAsync(HttpContext httpContext, LoginResult login)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.Name, login.Username),
            new Claim(AuthConstants.ApiTokenClaimType, login.Token)
        ];

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        return httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = login.ExpiresAtUtc
            });
    }

    // The message the login page shows when the API refuses the login.
    public static string FailureMessage<T>(Common.ApiResult<T> result) =>
        result.StatusCode == StatusCodes.Status401Unauthorized
            ? "Incorrect username or password."
            : result.ToDisplayMessage("Could not log in.");
}
