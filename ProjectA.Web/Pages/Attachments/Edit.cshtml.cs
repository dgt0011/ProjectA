using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Attachments;

public class EditModel(IAttachmentsApiClient attachmentsApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public AttachmentInput Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await attachmentsApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Attachment not found.");
            return RedirectToPage("Index");
        }

        Form = new AttachmentInput
        {
            Title = result.Value.Title,
            Description = result.Value.Description,
            S3Arn = result.Value.S3Arn
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await attachmentsApiClient.UpdateAsync(Id, Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not update the attachment."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Attachment updated.";
        return RedirectToPage("Index");
    }
}
