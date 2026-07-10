using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Attachments;

public class IndexModel(IAttachmentsApiClient attachmentsApiClient) : PageModel
{
    public List<AttachmentDto> Attachments { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await attachmentsApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Attachments = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await attachmentsApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Attachment deleted."
            : result.ToDisplayMessage("Could not delete the attachment.");

        return RedirectToPage();
    }
}
