using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog.Categories;

// List + inline "add" form; renaming is on the Edit page. Categories are also created on the
// fly by typing a new name into a post's category box, so this page is mostly for tidying up.
[Authorize]
public class IndexModel(IBlogCategoriesApiClient blogCategoriesApiClient) : PageModel
{
    [BindProperty]
    public BlogCategoryInput Form { get; set; } = new();

    public List<BlogCategoryDto> Categories { get; private set; } = [];
    public bool LoadedSuccessfully { get; private set; } = true;

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await blogCategoriesApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the category."));
            }

            await LoadAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "Blog category created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await blogCategoriesApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Blog category deleted."
            : result.ToDisplayMessage("Could not delete the category.");
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var result = await blogCategoriesApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Categories = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }
    }
}
