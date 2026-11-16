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

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
