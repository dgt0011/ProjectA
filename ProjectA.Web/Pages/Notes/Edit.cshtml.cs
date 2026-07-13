using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Notes;

[Authorize]
public class EditModel(
    INotesApiClient notesApiClient,
    IBookmarksApiClient bookmarksApiClient,
    IAttachmentsApiClient attachmentsApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public NoteInput Form { get; set; } = new();

    public List<BookmarkDto> AvailableBookmarks { get; set; } = [];
    public List<AttachmentDto> AvailableAttachments { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await notesApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Note not found.");
            return RedirectToPage("Index");
        }

        Form = new NoteInput
        {
            Title = result.Value.Title,
            Description = result.Value.Description,
            Body = result.Value.Body,
            BookmarkIds = [.. result.Value.BookmarkIds],
            AttachmentIds = [.. result.Value.AttachmentIds]
        };

        await LoadAssociationOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAssociationOptionsAsync(cancellationToken);
            return Page();
        }

        var result = await notesApiClient.UpdateAsync(Id, Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not update the note."));
            }

            await LoadAssociationOptionsAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "Note updated.";
        return RedirectToPage("Index");
    }

    private async Task LoadAssociationOptionsAsync(CancellationToken cancellationToken)
    {
        var bookmarksTask = bookmarksApiClient.GetListAsync(cancellationToken);
        var attachmentsTask = attachmentsApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(bookmarksTask, attachmentsTask);

        var bookmarksResult = await bookmarksTask;
        if (bookmarksResult.IsSuccess)
        {
            AvailableBookmarks = bookmarksResult.Value ?? [];
        }

        var attachmentsResult = await attachmentsTask;
        if (attachmentsResult.IsSuccess)
        {
            AvailableAttachments = attachmentsResult.Value ?? [];
        }
    }
}
