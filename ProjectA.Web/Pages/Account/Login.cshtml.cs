using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Security;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Account;

public class LoginModel(IAuthApiClient authApiClient) : PageModel
{
    [BindProperty]
    public LoginInput Form { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await authApiClient.LoginAsync(Form.Username, Form.Password, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.StatusCode == StatusCodes.Status401Unauthorized
                    ? "Incorrect username or password."
                    : result.ToDisplayMessage("Could not log in."));
            return Page();
        }

        // The token itself lives only as a claim on this cookie's encrypted identity - there is
        // no separate server-side session store. BearerTokenHandler reads it back out of
        // HttpContext.User on every outgoing call to ProjectA.Api.
        List<Claim> claims =
        [
            new Claim(ClaimTypes.Name, result.Value.Username),
            new Claim(AuthConstants.ApiTokenClaimType, result.Value.Token)
        ];

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = result.Value.ExpiresAtUtc
            });

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
    }
}
