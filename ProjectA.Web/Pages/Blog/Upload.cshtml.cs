using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;

namespace ProjectA.Web.Pages.Blog;

// Upload target for the post editor's drag/drop/paste image support (see _BlogEditorScripts).
// A Razor Page rather than a minimal API endpoint so it gets Razor Pages' automatic
// antiforgery validation for free. Responds in the JSON shape EasyMDE expects.
[Authorize]
[RequestSizeLimit(26 * 1024 * 1024)]
public class UploadModel(BlogFileStorage blogFileStorage) : PageModel
{
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync(IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null)
        {
            return BadRequest(new { error = "No file was uploaded." });
        }

        if (!BlogFileStorage.IsAllowed(image, out var error))
        {
            return BadRequest(new { error });
        }

        var path = await blogFileStorage.SaveAsync(image, cancellationToken);
        return new JsonResult(new { data = new { filePath = path } });
    }
}
