using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProjectA.Web.Pages.Account;

public class LogoutModel : PageModel
{
    // Logging out is only ever triggered via the POST form in _Layout.cshtml - visiting this
    // page directly with GET just shows a confirmation, it doesn't sign anyone out.
    public void OnGet()
    {
    }

    // returnUrl: where to go afterwards - the layout sends the blog login page when logging out
    // from the Blog admin, so blog users end up back on the blog-styled sign-in. Local only.
    public async Task<IActionResult> OnPostAsync([FromForm] string? returnUrl)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToPage("/Index");
    }
}
