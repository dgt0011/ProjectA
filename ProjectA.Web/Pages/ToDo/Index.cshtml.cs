using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.ToDo;

public class IndexModel(
    IToDoApiClient toDoApiClient,
    ICategoriesApiClient categoriesApiClient,
    IProjectsApiClient projectsApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public bool IncludeDone { get; set; }

    // Off by default - Project ToDos have their own place on their Project's Details page, so
    // this page hides them unless asked for, at which point they're shown in their own section
    // grouped by Project (then by Category within each Project).
    [BindProperty(SupportsGet = true)]
    public bool ShowProjectItems { get; set; }

    public List<ToDoDto> ToDoItems { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    // Items with no Project association only - uncategorized first (as their own group), then
    // categorized items grouped by category title - see ToDoGrouping. Always shown.
    public List<ToDoGrouping.ToDoGroup> GroupedToDoItems { get; set; } = [];

    // Items with a Project association, grouped by Project then Category - see ToDoGrouping.
    // Only populated (and only rendered) when ShowProjectItems is checked.
    public List<ToDoGrouping.ProjectToDoGroup> GroupedProjectToDoItems { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var toDoTask = toDoApiClient.GetListAsync(IncludeDone, cancellationToken: cancellationToken);
        var categoriesTask = categoriesApiClient.GetListAsync(cancellationToken);
        var projectsTask = ShowProjectItems ? projectsApiClient.GetListAsync(cancellationToken) : null;

        if (projectsTask is not null)
        {
            await Task.WhenAll(toDoTask, categoriesTask, projectsTask);
        }
        else
        {
            await Task.WhenAll(toDoTask, categoriesTask);
        }

        var result = await toDoTask;
        if (result.IsSuccess)
        {
            ToDoItems = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }

        var categoryTitlesById = new Dictionary<long, string>();
        var categoriesResult = await categoriesTask;
        if (categoriesResult.IsSuccess)
        {
            categoryTitlesById = (categoriesResult.Value ?? []).ToDictionary(category => category.Id, category => category.Title);
        }

        GroupedToDoItems = ToDoGrouping.GroupByCategory(
            ToDoItems.Where(todo => todo.ProjectId is null),
            categoryTitlesById);

        if (projectsTask is not null)
        {
            var projectTitlesById = new Dictionary<long, string>();
            var projectsResult = await projectsTask;
            if (projectsResult.IsSuccess)
            {
                projectTitlesById = (projectsResult.Value ?? []).ToDictionary(project => project.Id, project => project.Title);
            }

            GroupedProjectToDoItems = ToDoGrouping.GroupByProjectThenCategory(
                ToDoItems.Where(todo => todo.ProjectId is not null),
                categoryTitlesById,
                projectTitlesById);
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

        return RedirectToPage(new { includeDone = IncludeDone, showProjectItems = ShowProjectItems });
    }
}
