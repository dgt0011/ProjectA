using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Users;

// The signed-in user's public blog author details (what Blogifier kept on its Authors table).
[Authorize]
public class ProfileModel(IUsersApiClient usersApiClient, BlogFileStorage blogFileStorage) : PageModel
{
    [BindProperty]
    public UserProfileInput Form { get; set; } = new();

    [BindProperty]
    public IFormFile? AvatarFile { get; set; }

    public string Username { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await usersApiClient.GetProfileAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Could not load your profile.");
            return RedirectToPage("/Blog/Index");
        }

        Username = result.Value.Username;
        Form = new UserProfileInput
        {
            DisplayName = result.Value.DisplayName,
            Bio = result.Value.Bio,
            Avatar = result.Value.Avatar
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Username = User.Identity?.Name ?? string.Empty;

        if (AvatarFile is not null)
        {
            if (BlogFileStorage.IsAllowed(AvatarFile, out var error))
            {
                Form.Avatar = await blogFileStorage.SaveAsync(AvatarFile, cancellationToken);
            }
            else
            {
                ModelState.AddModelError(nameof(AvatarFile), error);
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await usersApiClient.UpdateProfileAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not save your profile."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Profile saved.";
        return RedirectToPage();
    }
}
