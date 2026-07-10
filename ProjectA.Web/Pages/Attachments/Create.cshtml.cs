using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Attachments;

public class CreateModel(IAttachmentsApiClient attachmentsApiClient) : PageModel
{
    [BindProperty]
    public AttachmentInput Form { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await attachmentsApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the attachment."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Attachment created.";
        return RedirectToPage("Index");
    }
}
