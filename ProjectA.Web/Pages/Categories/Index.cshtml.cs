using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Categories;

public class IndexModel(ICategoriesApiClient categoriesApiClient) : PageModel
{
    public List<CategoryDto> Categories { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await categoriesApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Categories = result.Value ?? [];
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
            // public GET handler with this protected POST handler, so unlike Create/Edit
            // (which are [Authorize] at the whole PageModel) this checks per-handler and
            // sends anonymous callers to the login page instead.
            return Challenge();
        }

        var result = await categoriesApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Category deleted."
            : result.ToDisplayMessage("Could not delete the category.");

        return RedirectToPage();
    }
}
