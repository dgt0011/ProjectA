using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Attachments;

public class IndexModel(IAttachmentsApiClient attachmentsApiClient, IAttachmentTypesApiClient attachmentTypesApiClient) : PageModel
{
    public List<AttachmentDto> Attachments { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    private Dictionary<long, AttachmentTypeDto> _attachmentTypesById = [];

    // Null when the attachment has no AttachmentTypeId, or the type it referred to no longer
    // exists - callers treat both cases the same way (no icon, no row color).
    public AttachmentTypeDto? AttachmentType(long? attachmentTypeId) =>
        attachmentTypeId is long id ? _attachmentTypesById.GetValueOrDefault(id) : null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var attachmentsTask = attachmentsApiClient.GetListAsync(cancellationToken);
        var attachmentTypesTask = attachmentTypesApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(attachmentsTask, attachmentTypesTask);

        var result = await attachmentsTask;
        if (result.IsSuccess)
        {
            Attachments = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }

        var attachmentTypesResult = await attachmentTypesTask;
        if (attachmentTypesResult.IsSuccess)
        {
            _attachmentTypesById = (attachmentTypesResult.Value ?? []).ToDictionary(attachmentType => attachmentType.Id);
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

        var result = await attachmentsApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Attachment deleted."
            : result.ToDisplayMessage("Could not delete the attachment.");

        return RedirectToPage();
    }
}
