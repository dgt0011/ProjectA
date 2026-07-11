using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Users;

// The whole page requires auth (not just the delete handler) - unlike the other entities'
// Index pages, there is no public view here at all, matching /api/users on the API side.
[Authorize]
public class IndexModel(IUsersApiClient usersApiClient) : PageModel
{
    public List<UserDto> Users { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await usersApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Users = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await usersApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "User deleted."
            : result.ToDisplayMessage("Could not delete the user.");

        return RedirectToPage();
    }
}
