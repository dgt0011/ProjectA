using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.BookmarkTypes;

public class IndexModel(IBookmarkTypesApiClient bookmarkTypesApiClient) : PageModel
{
    public List<BookmarkTypeDto> BookmarkTypes { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await bookmarkTypesApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            BookmarkTypes = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            // Index/list stays anonymous, but deleting is a write - this page mixes a
            // public GET handler with this protected POST handler, same convention as
            // Categories/Bookmarks Index.
            return Challenge();
        }

        var result = await bookmarkTypesApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Bookmark type deleted."
            : result.ToDisplayMessage("Could not delete the bookmark type.");

        return RedirectToPage();
    }
}
