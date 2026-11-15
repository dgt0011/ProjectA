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
        if (CoverFile is not null)
        {
            if (BlogFileStorage.IsAllowed(CoverFile, out var error))
            {
                Form.Cover = await blogFileStorage.SaveAsync(CoverFile, cancellationToken);
            }
            else
            {
                ModelState.AddModelError(nameof(CoverFile), error);
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
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
