using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog;

[Authorize]
public class SettingsModel(IBlogAdminApiClient blogAdminApiClient, BlogFileStorage blogFileStorage) : PageModel
{
    [BindProperty]
    public BlogSettingsInput Form { get; set; } = new();

    [BindProperty]
    public IFormFile? CoverFile { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await blogAdminApiClient.GetSettingsAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Could not load blog settings.");
            return RedirectToPage("Index");
        }

        var s = result.Value;
        Form = new BlogSettingsInput
        {
            Title = s.Title,
            Description = s.Description,
            ItemsPerPage = s.ItemsPerPage,
            IncludeFeatured = s.IncludeFeatured,
            Cover = s.Cover,
            Logo = s.Logo,
            HeaderScript = s.HeaderScript,
            FooterScript = s.FooterScript
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (CoverFile is not null && !BlogFileStorage.IsAllowed(CoverFile, out var error))
        {
            ModelState.AddModelError(nameof(CoverFile), error);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Saved only once the form is otherwise valid, so a rejected form leaves no stray public
        // file. If the API save still fails, the new path is shown in the Cover box (ModelState
        // would otherwise re-show the posted value) and a resubmit reuses the same file.
        if (CoverFile is not null)
        {
            Form.Cover = await blogFileStorage.SaveAsync(CoverFile, cancellationToken);
            ModelState.Remove($"{nameof(Form)}.{nameof(Form.Cover)}");
        }

        var result = await blogAdminApiClient.UpdateSettingsAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not save blog settings."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Blog settings saved.";
        return RedirectToPage();
    }
}
