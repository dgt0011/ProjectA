using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Users;

// Self-service - changes the signed-in caller's own password. There is no route parameter for
// a user Id: IUsersApiClient.ChangePasswordAsync always acts on whichever account the current
// bearer token (attached automatically by BearerTokenHandler) belongs to.
[Authorize]
public class ChangePasswordModel(IUsersApiClient usersApiClient) : PageModel
{
    [BindProperty]
    public ChangePasswordInput Form { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await usersApiClient.ChangePasswordAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not change the password."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Your password has been changed.";
        return RedirectToPage("Index");
    }
}
