using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Projects;

public class EditModel(IProjectsApiClient projectsApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public ProjectInput Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await projectsApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Project not found.");
            return RedirectToPage("Index");
        }

        Form = new ProjectInput
        {
            Title = result.Value.Title,
            Description = result.Value.Description,
            StartDate = DateOnly.FromDateTime(result.Value.StartDate)
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await projectsApiClient.UpdateAsync(Id, Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not update the project."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Project updated.";
        return RedirectToPage("Index");
    }
}
