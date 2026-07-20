using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.AttachmentTypes;

[Authorize]
public class EditModel(IAttachmentTypesApiClient attachmentTypesApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public AttachmentTypeInput Form { get; set; } = new();

    // Bound separately from Form because asp-for can't bind an <input type="file"> to a
    // string - this is converted to Form.IconBase64/IconContentType before the API call.
    [BindProperty]
    public IFormFile? IconFile { get; set; }

    public bool HasIcon { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await attachmentTypesApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Attachment type not found.");
            return RedirectToPage("Index");
        }

        Form = new AttachmentTypeInput { Title = result.Value.Title, Color = result.Value.Color };
        HasIcon = result.Value.HasIcon;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await RefreshHasIconAsync(cancellationToken);
            return Page();
        }

        if (IconFile is { Length: > 0 })
        {
            using var stream = new MemoryStream();
            await IconFile.CopyToAsync(stream, cancellationToken);
            Form.IconBase64 = Convert.ToBase64String(stream.ToArray());
            Form.IconContentType = IconFile.ContentType;
        }

        var result = await attachmentTypesApiClient.UpdateAsync(Id, Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not update the attachment type."));
            }

            await RefreshHasIconAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "Attachment type updated.";
        return RedirectToPage("Index");
    }

    // Re-fetches HasIcon for redisplaying the page after a validation failure - the icon
    // itself isn't part of Form, so it wouldn't otherwise survive the round trip.
    private async Task RefreshHasIconAsync(CancellationToken cancellationToken)
    {
        var result = await attachmentTypesApiClient.GetByIdAsync(Id, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            HasIcon = result.Value.HasIcon;
        }
    }
}
