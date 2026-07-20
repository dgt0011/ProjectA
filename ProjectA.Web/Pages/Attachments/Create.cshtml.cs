using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Attachments;

[Authorize]
public class CreateModel(
    IAttachmentsApiClient attachmentsApiClient,
    IAttachmentTypesApiClient attachmentTypesApiClient,
    IWebHostEnvironment webHostEnvironment) : PageModel
{
    [BindProperty]
    public AttachmentInput Form { get; set; } = new();

    // Bound separately from Form because asp-for can't bind an <input type="file"> to a
    // string - Form.FilePath isn't typed in on this page at all, it's derived from this file
    // once it's saved to wwwroot/files (see AttachmentFileStorage), right before the API call.
    [BindProperty]
    public IFormFile? UploadedFile { get; set; }

    public List<AttachmentTypeDto> AvailableAttachmentTypes { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAttachmentTypesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Nothing on this page ever binds Form.FilePath, so its [Required] attribute always
        // fails model validation - clear that one error out and require UploadedFile instead,
        // which is what actually determines FilePath a few lines down.
        ModelState.Remove($"{nameof(Form)}.{nameof(Form.FilePath)}");

        if (UploadedFile is not { Length: > 0 })
        {
            ModelState.AddModelError(nameof(UploadedFile), "Please choose a file to upload.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAttachmentTypesAsync(cancellationToken);
            return Page();
        }

        // Note: if CreateAsync below fails (e.g. an API-side error), the file saved here is
        // left orphaned on disk rather than cleaned up - an accepted, narrow edge case rather
        // than a distributed-transaction problem worth solving for this app.
        Form.FilePath = await AttachmentFileStorage.SaveAsync(webHostEnvironment, UploadedFile!, cancellationToken);

        var result = await attachmentsApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the attachment."));
            }

            await LoadAttachmentTypesAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "Attachment created.";
        return RedirectToPage("Index");
    }

    private async Task LoadAttachmentTypesAsync(CancellationToken cancellationToken)
    {
        var result = await attachmentTypesApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            AvailableAttachmentTypes = result.Value ?? [];
        }
    }
}
