using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Projects;

[Authorize]
public class CreateModel(IProjectsApiClient projectsApiClient) : PageModel
{
    [BindProperty]
    public ProjectInput Form { get; set; } = new() { StartDate = DateOnly.FromDateTime(DateTime.Today) };

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await projectsApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the project."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Project created.";
        return RedirectToPage("Index");
    }
}
