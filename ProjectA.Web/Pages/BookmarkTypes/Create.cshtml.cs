using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.BookmarkTypes;

[Authorize]
public class CreateModel(IBookmarkTypesApiClient bookmarkTypesApiClient) : PageModel
{
    [BindProperty]
    public BookmarkTypeInput Form { get; set; } = new();

    // Bound separately from Form because asp-for can't bind an <input type="file"> to a
    // string - this is converted to Form.IconBase64/IconContentType before the API call.
    [BindProperty]
    public IFormFile? IconFile { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (IconFile is { Length: > 0 })
        {
            using var stream = new MemoryStream();
            await IconFile.CopyToAsync(stream, cancellationToken);
            Form.IconBase64 = Convert.ToBase64String(stream.ToArray());
            Form.IconContentType = IconFile.ContentType;
        }

        var result = await bookmarkTypesApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the bookmark type."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Bookmark type created.";
        return RedirectToPage("Index");
    }
}
