using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Bookmarks;

[Authorize]
public class CreateModel(
    IBookmarksApiClient bookmarksApiClient,
    ICategoriesApiClient categoriesApiClient,
    IBookmarkTypesApiClient bookmarkTypesApiClient) : PageModel
{
    [BindProperty]
    public BookmarkInput Form { get; set; } = new();

    public List<CategoryDto> AvailableCategories { get; set; } = [];
    public List<BookmarkTypeDto> AvailableBookmarkTypes { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(cancellationToken);
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

            await LoadOptionsAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "Bookmark created.";
        return RedirectToPage("Index");
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var categoriesTask = categoriesApiClient.GetListAsync(cancellationToken);
        var bookmarkTypesTask = bookmarkTypesApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(categoriesTask, bookmarkTypesTask);

        var categoriesResult = await categoriesTask;
        if (categoriesResult.IsSuccess)
        {
            AvailableCategories = categoriesResult.Value ?? [];
        }

        var bookmarkTypesResult = await bookmarkTypesTask;
        if (bookmarkTypesResult.IsSuccess)
        {
            AvailableBookmarkTypes = bookmarkTypesResult.Value ?? [];
        }
    }
}
