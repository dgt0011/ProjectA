using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.ToDo;

public class CreateModel(IToDoApiClient toDoApiClient, ICategoriesApiClient categoriesApiClient) : PageModel
{
    [BindProperty]
    public ToDoCreateInput Form { get; set; } = new();

    public List<string> CategorySuggestions { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadCategorySuggestionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategorySuggestionsAsync(cancellationToken);
            return Page();
        }

        var result = await toDoApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the ToDo item."));
            }

            await LoadCategorySuggestionsAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "ToDo item created.";
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
