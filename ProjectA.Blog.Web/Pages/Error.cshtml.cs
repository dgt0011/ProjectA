using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProjectA.Blog.Web.Pages;

// Target of both UseStatusCodePagesWithReExecute (404s etc., ?code=...) and UseExceptionHandler.
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public bool IsNotFound { get; private set; }

    public void OnGet(int? code) => IsNotFound = (code ?? Response.StatusCode) == StatusCodes.Status404NotFound;

    public void OnPost(int? code) => OnGet(code);
}
