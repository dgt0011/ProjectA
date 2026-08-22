using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.ToDo;

public class IndexModel(IToDoApiClient toDoApiClient, ICategoriesApiClient categoriesApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public bool IncludeDone { get; set; }

    public List<ToDoDto> ToDoItems { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    private Dictionary<long, string> _categoryTitlesById = [];

    public string CategoryTitle(long? categoryId) =>
        categoryId is null
            ? "-"
            : _categoryTitlesById.GetValueOrDefault(categoryId.Value, $"#{categoryId}");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var toDoTask = toDoApiClient.GetListAsync(IncludeDone, cancellationToken: cancellationToken);
        var categoriesTask = categoriesApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(toDoTask, categoriesTask);

        var result = await toDoTask;
        if (result.IsSuccess)
        {
            ToDoItems = result.Value ?? [];
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

        var result = await toDoApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "ToDo item deleted."
            : result.ToDisplayMessage("Could not delete the ToDo item.");

        return RedirectToPage(new { includeDone = IncludeDone });
    }
}
