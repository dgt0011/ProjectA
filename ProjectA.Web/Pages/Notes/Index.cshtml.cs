using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Notes;

public class IndexModel(INotesApiClient notesApiClient, ICategoriesApiClient categoriesApiClient) : PageModel
{
    private Dictionary<long, string> _categoryTitlesById = [];

    public List<NoteDto> Notes { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    // Only Categories actually attached to at least one Note in the list - the Filter
    // input's autocomplete offers these, not every Category in the system, so it never
    // suggests a Category that couldn't possibly match anything on this page.
    public List<CategoryDto> UsedCategories { get; set; } = [];

    public string CategoryTitle(long categoryId) =>
        _categoryTitlesById.GetValueOrDefault(categoryId, $"#{categoryId}");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var notesTask = notesApiClient.GetListAsync(cancellationToken);
        var categoriesTask = categoriesApiClient.GetListAsync(cancellationToken);

        //TODO: This seems ... redundant?  We're awaiting the results of each task below anyway?
        await Task.WhenAll(notesTask, categoriesTask);

        var notesResult = await notesTask;

        Notes = notesResult.IsSuccess ? notesResult.Value ?? [] : [];
        if (!notesResult.IsSuccess)
        {
            LoadedSuccessfully = false;
        }

        var categoriesResult = await categoriesTask;
        var categories = categoriesResult.IsSuccess ? categoriesResult.Value ?? [] : [];
        _categoryTitlesById = categories.ToDictionary(category => category.Id, category => category.Title);

        var usedCategoryIds = Notes.SelectMany(note => note.CategoryIds).ToHashSet();
        UsedCategories = categories
            .Where(category => usedCategoryIds.Contains(category.Id))
            .OrderBy(category => category.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        var result = await notesApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Note deleted."
            : result.ToDisplayMessage("Could not delete the note.");

        return RedirectToPage();
    }
}
