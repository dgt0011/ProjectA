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
        var result = await bookmarksApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Bookmark deleted."
            : result.ToDisplayMessage("Could not delete the bookmark.");

        return RedirectToPage();
    }
}
