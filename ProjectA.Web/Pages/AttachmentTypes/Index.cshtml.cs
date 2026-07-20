using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.AttachmentTypes;

public class IndexModel(IAttachmentTypesApiClient attachmentTypesApiClient) : PageModel
{
    public List<AttachmentTypeDto> AttachmentTypes { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await attachmentTypesApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            AttachmentTypes = result.Value ?? [];
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
            // public GET handler with this protected POST handler, same convention as
            // Categories/Bookmarks/BookmarkTypes Index.
            return Challenge();
        }

        var result = await attachmentTypesApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Attachment type deleted."
            : result.ToDisplayMessage("Could not delete the attachment type.");

        return RedirectToPage();
    }
}
