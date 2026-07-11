using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Bookmarks;

public class IndexModel(IBookmarksApiClient bookmarksApiClient, ICategoriesApiClient categoriesApiClient) : PageModel
{
    public List<BookmarkDto> Bookmarks { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    private Dictionary<long, string> _categoryTitlesById = [];

    public string CategoryTitle(long categoryId) =>
        _categoryTitlesById.GetValueOrDefault(categoryId, $"#{categoryId}");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var bookmarksTask = bookmarksApiClient.GetListAsync(cancellationToken);
        var categoriesTask = categoriesApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(bookmarksTask, categoriesTask);

        var bookmarksResult = await bookmarksTask;
        if (bookmarksResult.IsSuccess)
        {
            Bookmarks = bookmarksResult.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }

        var categoriesResult = await categoriesTask;
        if (categoriesResult.IsSuccess)
        {
            _categoryTitlesById = (categoriesResult.Value ?? []).ToDictionary(category => category.Id, category => category.Title);
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            // Index/list stays anonymous, but deleting is a write - this page mixes a
            // public GET handler with this protected POST handler, so unlike Create/Edit
            // (which are [Authorize] at the whole PageModel) this checks per-handler and
            // sends anonymous callers to the login page instead.
            return Challenge();
        }

        var result = await bookmarksApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Bookmark deleted."
            : result.ToDisplayMessage("Could not delete the bookmark.");

        return RedirectToPage();
    }
}
