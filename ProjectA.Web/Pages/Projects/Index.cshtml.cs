using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Projects;

public class IndexModel(IProjectsApiClient projectsApiClient) : PageModel
{
    public List<ProjectDto> Projects { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await projectsApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Projects = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await projectsApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Project deleted."
            : result.ToDisplayMessage("Could not delete the project.");

        return RedirectToPage();
    }
}
