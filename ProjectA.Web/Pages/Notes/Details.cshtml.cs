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
    IAttachmentsApiClient attachmentsApiClient,
    IBookmarkTypesApiClient bookmarkTypesApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public NoteDto? Note { get; set; }

    // GetNoteByIdEndpoint only returns the associated ids, not full bookmark/attachment
    // details - the id -> summary lookup happens here, the same way Bookmarks' own Index
    // page resolves CategoryIds to titles.
    public List<BookmarkDto> AssociatedBookmarks { get; set; } = [];
    public List<AttachmentDto> AssociatedAttachments { get; set; } = [];

    private Dictionary<long, BookmarkTypeDto> _bookmarkTypesById = [];

    // Null when the bookmark has no BookmarkTypeId, or the type it referred to no longer
    // exists - same helper/semantics as Bookmarks' own Index page (BookmarkType(...)) so the
    // icon/row-color treatment stays identical wherever a bookmark is listed.
    public BookmarkTypeDto? BookmarkType(long? bookmarkTypeId) =>
        bookmarkTypeId is long id ? _bookmarkTypesById.GetValueOrDefault(id) : null;

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
            var bookmarksTask = bookmarksApiClient.GetListAsync(cancellationToken);
            var bookmarkTypesTask = bookmarkTypesApiClient.GetListAsync(cancellationToken);
            await Task.WhenAll(bookmarksTask, bookmarkTypesTask);

            var bookmarksResult = await bookmarksTask;
            if (bookmarksResult.IsSuccess)
            {
                var bookmarkIds = Note.BookmarkIds.ToHashSet();
                AssociatedBookmarks = (bookmarksResult.Value ?? [])
                    .Where(bookmark => bookmarkIds.Contains(bookmark.Id))
                    .ToList();
            }

            var bookmarkTypesResult = await bookmarkTypesTask;
            if (bookmarkTypesResult.IsSuccess)
            {
                _bookmarkTypesById = (bookmarkTypesResult.Value ?? []).ToDictionary(bookmarkType => bookmarkType.Id);
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
