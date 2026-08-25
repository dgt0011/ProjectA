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
    IAttachmentsApiClient attachmentsApiClient,
    ICategoriesApiClient categoriesApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    // Set (e.g. by Projects/Details' per-Note "Edit" links) so saving here returns the user to
    // wherever they came from instead of always landing on the Notes list. Bound from the query
    // string on GET, then round-tripped through the POST via a hidden field on the form.
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    // Validated once so both the Cancel link and the post-save redirect use the same,
    // already-checked target - Url.IsLocalUrl guards against ReturnUrl (an ordinary,
    // user-controllable query string value) being used as an open-redirect vector.
    public string SafeReturnUrl => ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Page("Index")!;

    [BindProperty]
    public NoteInput Form { get; set; } = new();

    public List<NoteDto> AvailableParentNotes { get; set; } = [];
    public List<BookmarkDto> AvailableBookmarks { get; set; } = [];
    public List<AttachmentDto> AvailableAttachments { get; set; } = [];
    
    public List<CategoryDto> AvailableCategories { get; set; } = [];

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
            ParentNoteId = result.Value.ParentNoteId,
            IsPrivate = result.Value.IsPrivate,
            BookmarkIds = [.. result.Value.BookmarkIds],
            AttachmentIds = [.. result.Value.AttachmentIds],
            CategoryIds = [.. result.Value.CategoryIds],
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
        return LocalRedirect(SafeReturnUrl);
    }

    private async Task LoadAssociationOptionsAsync(CancellationToken cancellationToken)
    {
        var notesTask = notesApiClient.GetListAsync(cancellationToken);
        var bookmarksTask = bookmarksApiClient.GetListAsync(cancellationToken);
        var attachmentsTask = attachmentsApiClient.GetListAsync(cancellationToken);
        var categoriesTask = categoriesApiClient.GetListAsync(cancellationToken);
        
        await Task.WhenAll(notesTask, bookmarksTask, attachmentsTask, categoriesTask);

        var notesResult = await notesTask;
        if (notesResult.IsSuccess)
        {
            var allNotes = notesResult.Value ?? [];

            // Exclude this note itself and any of its descendants - the API rejects these
            // as a ParentNoteId anyway (they'd create a cycle), but filtering them out of
            // the dropdown here avoids offering choices that would just bounce back with a
            // validation error.
            var excluded = new HashSet<long> { Id };
            var addedMore = true;
            while (addedMore)
            {
                addedMore = false;
                foreach (var note in allNotes)
                {
                    if (note.ParentNoteId is { } parentId && excluded.Contains(parentId) && excluded.Add(note.Id))
                    {
                        addedMore = true;
                    }
                }
            }

            AvailableParentNotes = allNotes.Where(note => !excluded.Contains(note.Id)).ToList();
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
        
        var categoriesResult = await categoriesTask;
        if (categoriesResult.IsSuccess)
        {
            AvailableCategories = categoriesResult.Value ?? [];
        }
    }
}
