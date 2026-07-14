using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.ToDo;

[Authorize]
public class EditModel(IToDoApiClient toDoApiClient, ICategoriesApiClient categoriesApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public ToDoEditInput Form { get; set; } = new();

    public List<CategoryDto> AvailableCategories { get; set; } = [];

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
            CategoryId = result.Value.CategoryId,
            Description = result.Value.Description,
            Done = result.Value.Done
        };

        await LoadCategoriesAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(cancellationToken);
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

            await LoadCategoriesAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "ToDo item updated.";
        return RedirectToPage("Index");
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var result = await categoriesApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            AvailableCategories = result.Value ?? [];
        }
    }
}
