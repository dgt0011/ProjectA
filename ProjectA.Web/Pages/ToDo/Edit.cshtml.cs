using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.ToDo;

public class EditModel(IToDoApiClient toDoApiClient, ICategoriesApiClient categoriesApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public ToDoEditInput Form { get; set; } = new();

    public List<string> CategorySuggestions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await toDoApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("ToDo item not found.");
            return RedirectToPage("Index");
        }

        Form = new ToDoEditInput
        {
            Title = result.Value.Title,
            Category = result.Value.Category,
            Description = result.Value.Description,
            Done = result.Value.Done
        };

        await LoadCategorySuggestionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategorySuggestionsAsync(cancellationToken);
            return Page();
        }

        var result = await toDoApiClient.UpdateAsync(Id, Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not update the ToDo item."));
            }

            await LoadCategorySuggestionsAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "ToDo item updated.";
        return RedirectToPage("Index");
    }

    private async Task LoadCategorySuggestionsAsync(CancellationToken cancellationToken)
    {
        var categories = await categoriesApiClient.GetListAsync(cancellationToken);
        if (categories.IsSuccess)
        {
            CategorySuggestions = (categories.Value ?? [])
                .Select(category => category.Title)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
