using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Bookmarks;

public class IndexModel(
    IBookmarksApiClient bookmarksApiClient,
    ICategoriesApiClient categoriesApiClient,
    IBookmarkTypesApiClient bookmarkTypesApiClient) : PageModel
{
    public List<BookmarkCategoryGroup> CategoryGroups { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    private Dictionary<long, string> _categoryTitlesById = [];
    private Dictionary<long, BookmarkTypeDto> _bookmarkTypesById = [];

    public string CategoryTitle(long categoryId) =>
        _categoryTitlesById.GetValueOrDefault(categoryId, $"#{categoryId}");

    // Null when the bookmark has no BookmarkTypeId, or the type it referred to no longer
    // exists - callers treat both cases the same way (no icon, no row color).
    public BookmarkTypeDto? BookmarkType(long? bookmarkTypeId) =>
        bookmarkTypeId is long id ? _bookmarkTypesById.GetValueOrDefault(id) : null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var bookmarksTask = bookmarksApiClient.GetListAsync(cancellationToken);
        var categoriesTask = categoriesApiClient.GetListAsync(cancellationToken);
        var bookmarkTypesTask = bookmarkTypesApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(bookmarksTask, categoriesTask, bookmarkTypesTask);

        var bookmarksResult = await bookmarksTask;
        var bookmarks = bookmarksResult.IsSuccess ? bookmarksResult.Value ?? [] : [];
        if (!bookmarksResult.IsSuccess)
        {
            LoadedSuccessfully = false;
        }

        HasAnyBookmarks = bookmarks.Count > 0;

        var categoriesResult = await categoriesTask;
        var categories = categoriesResult.IsSuccess ? categoriesResult.Value ?? [] : [];
        _categoryTitlesById = categories.ToDictionary(category => category.Id, category => category.Title);

        var bookmarkTypesResult = await bookmarkTypesTask;
        if (bookmarkTypesResult.IsSuccess)
        {
            _bookmarkTypesById = (bookmarkTypesResult.Value ?? []).ToDictionary(bookmarkType => bookmarkType.Id);
        }

        // Every Category gets its own accordion section, even ones with no matching
        // Bookmarks - a Bookmark with more than one Category ends up listed under more than
        // one section, which is expected since CategoryIds is a many-to-many association.
        var groups = categories
            .Select(category => new BookmarkCategoryGroup
            {
                CategoryId = category.Id,
                CategoryTitle = category.Title,
                Bookmarks = bookmarks.Where(bookmark => bookmark.CategoryIds.Contains(category.Id)).ToList()
            })
            .ToList();

        var uncategorized = bookmarks.Where(bookmark => bookmark.CategoryIds.Count == 0).ToList();
        if (uncategorized.Count > 0)
        {
            groups.Add(new BookmarkCategoryGroup
            {
                CategoryId = null,
                CategoryTitle = "Uncategorized",
                Bookmarks = uncategorized
            });
        }

        CategoryGroups = groups;
    }

    public bool HasAnyBookmarks { get; set; }

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
