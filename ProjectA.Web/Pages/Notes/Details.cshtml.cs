using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Notes;

// Deliberately not [Authorize] - anyone can view a note's full content (matching
// GetNoteByIdEndpoint, which is anonymous on the API side too). Only the Edit link shown
// on the page is gated on the viewer being signed in.
public class DetailsModel(
    INotesApiClient notesApiClient,
    IBookmarksApiClient bookmarksApiClient,
    IAttachmentsApiClient attachmentsApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public NoteDto? Note { get; set; }

    // GetNoteByIdEndpoint only returns the associated ids, not full bookmark/attachment
    // details - the id -> summary lookup happens here, the same way Bookmarks' own Index
    // page resolves CategoryIds to titles.
    public List<BookmarkDto> AssociatedBookmarks { get; set; } = [];
    public List<AttachmentDto> AssociatedAttachments { get; set; } = [];

    // Built once from the full notes list so the recursive _NoteTreeItem partial can look up
    // any note's direct children without re-fetching. Only Note's own descendants ever get
    // rendered, but it's simplest to build the lookup for every note up front.
    public IReadOnlyDictionary<long, List<NoteDto>> ChildrenByParentId { get; set; } =
        new Dictionary<long, List<NoteDto>>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await notesApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Note not found.");
            return RedirectToPage("Index");
        }

        Note = result.Value;

        var allNotesResult = await notesApiClient.GetListAsync(cancellationToken);
        if (allNotesResult.IsSuccess)
        {
            ChildrenByParentId = (allNotesResult.Value ?? [])
                .Where(note => note.ParentNoteId is not null)
                .OrderBy(note => note.DateCreated)
                .GroupBy(note => note.ParentNoteId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        if (Note.BookmarkIds.Count > 0)
        {
            var bookmarksResult = await bookmarksApiClient.GetListAsync(cancellationToken);
            if (bookmarksResult.IsSuccess)
            {
                var bookmarkIds = Note.BookmarkIds.ToHashSet();
                AssociatedBookmarks = (bookmarksResult.Value ?? [])
                    .Where(bookmark => bookmarkIds.Contains(bookmark.Id))
                    .ToList();
            }
        }

        if (Note.AttachmentIds.Count > 0)
        {
            var attachmentsResult = await attachmentsApiClient.GetListAsync(cancellationToken);
            if (attachmentsResult.IsSuccess)
            {
                var attachmentIds = Note.AttachmentIds.ToHashSet();
                AssociatedAttachments = (attachmentsResult.Value ?? [])
                    .Where(attachment => attachmentIds.Contains(attachment.Id))
                    .ToList();
            }
        }

        return Page();
    }
}
