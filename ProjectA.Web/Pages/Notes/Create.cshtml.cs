using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Notes;

[Authorize]
public class CreateModel(
    INotesApiClient notesApiClient,
    IBookmarksApiClient bookmarksApiClient,
    IAttachmentsApiClient attachmentsApiClient) : PageModel
{
    [BindProperty]
    public NoteInput Form { get; set; } = new();

    public List<NoteDto> AvailableParentNotes { get; set; } = [];
    public List<BookmarkDto> AvailableBookmarks { get; set; } = [];
    public List<AttachmentDto> AvailableAttachments { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAssociationOptionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAssociationOptionsAsync(cancellationToken);
            return Page();
        }

        var result = await notesApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the note."));
            }

            await LoadAssociationOptionsAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "Note created.";
        return RedirectToPage("Index");
    }

    private async Task LoadAssociationOptionsAsync(CancellationToken cancellationToken)
    {
        var notesTask = notesApiClient.GetListAsync(cancellationToken);
        var bookmarksTask = bookmarksApiClient.GetListAsync(cancellationToken);
        var attachmentsTask = attachmentsApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(notesTask, bookmarksTask, attachmentsTask);

        // A brand-new note can't yet be anyone's ancestor, so every existing note is a
        // valid parent choice here (unlike Edit, which excludes self/descendants).
        var notesResult = await notesTask;
        if (notesResult.IsSuccess)
        {
            AvailableParentNotes = notesResult.Value ?? [];
        }

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
