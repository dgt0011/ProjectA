using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Security;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog;

// The blog's own sign-in page, styled like the public blog (www.darrentuer.net) rather than
// ProjectA, so someone arriving from the blog's login button never sees ProjectA's login.
// The blog's /admin/login/ link sends readers to the Blog admin, and the cookie handler in
// Program.cs sends any unauthenticated request under /Blog here instead of /Account/Login.
// Same account and same cookie as ProjectA's login - only the page differs.
[AllowAnonymous]
public class LoginModel(IAuthApiClient authApiClient, IBlogAdminApiClient blogAdminApiClient, IConfiguration configuration) : PageModel
{
    [BindProperty]
    public LoginInput Form { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BlogTitle { get; private set; } = "Blog";

    public string? BlogDescription { get; private set; }

    // The public blog's address: its stylesheet is loaded from there (one copy of the theme),
    // and the header title links back to it.
    public string? BlogSiteUrl => configuration["BlogSite:BaseUrl"]?.TrimEnd('/');

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl);
        }

        await LoadSiteAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadSiteAsync(cancellationToken);
            return Page();
        }

        var result = await authApiClient.LoginAsync(Form.Username, Form.Password, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, ApiCookieSignIn.FailureMessage(result));
            await LoadSiteAsync(cancellationToken);
            return Page();
        }

        await ApiCookieSignIn.SignInAsync(HttpContext, result.Value);
        return LocalRedirect(SafeReturnUrl);
    }

    // Only local URLs, and the Blog admin by default.
    private string SafeReturnUrl => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page("/Blog/Index")!;

    private async Task LoadSiteAsync(CancellationToken cancellationToken)
    {
        // Anonymous endpoint - works before sign-in. Falls back to a generic title if the API is down.
        var site = await blogAdminApiClient.GetSiteAsync(cancellationToken);
        if (site is { IsSuccess: true, Value: not null })
        {
            BlogTitle = string.IsNullOrWhiteSpace(site.Value.Title) ? BlogTitle : site.Value.Title;
            BlogDescription = site.Value.Description;
        }
    }
}
