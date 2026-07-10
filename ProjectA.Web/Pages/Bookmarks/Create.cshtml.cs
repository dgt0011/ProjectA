using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Bookmarks;

public class CreateModel(IBookmarksApiClient bookmarksApiClient, ICategoriesApiClient categoriesApiClient) : PageModel
{
    [BindProperty]
    public BookmarkInput Form { get; set; } = new();

    public List<CategoryDto> AvailableCategories { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadCategoriesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(cancellationToken);
            return Page();
        }

        var result = await bookmarksApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the bookmark."));
            }

            await LoadCategoriesAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "Bookmark created.";
        return RedirectToPage("Index");
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var result = await categoriesApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            AvailableCategories = result.Value ?? [];
        }
    }
}
