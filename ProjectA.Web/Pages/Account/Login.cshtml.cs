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
            ModelState.AddModelError(string.Empty, ApiCookieSignIn.FailureMessage(result));
            return Page();
        }

        await ApiCookieSignIn.SignInAsync(HttpContext, result.Value);
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
    }
}
