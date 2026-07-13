using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.ToDo;

[Authorize]
public class CreateModel(IToDoApiClient toDoApiClient, ICategoriesApiClient categoriesApiClient) : PageModel
{
    [BindProperty]
    public ToDoCreateInput Form { get; set; } = new();

    public List<CategoryDto> AvailableCategories { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadCategoriesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(cancellationToken);
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

            await LoadCategoriesAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "ToDo item created.";
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
