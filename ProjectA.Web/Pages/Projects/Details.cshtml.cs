using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Projects;

// Deliberately not [Authorize] - anyone can view a project's full content (matching
// GetProjectByIdEndpoint, which is anonymous on the API side too). Only the Edit link shown
// on the page is gated on the viewer being signed in.
public class DetailsModel(
    IProjectsApiClient projectsApiClient,
    INotesApiClient notesApiClient,
    IBookmarksApiClient bookmarksApiClient,
    IAttachmentsApiClient attachmentsApiClient,
    IBookmarkTypesApiClient bookmarkTypesApiClient,
    IAttachmentTypesApiClient attachmentTypesApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public ProjectDto? Project { get; set; }

    // GetProjectByIdEndpoint only returns the associated ids, not full note/bookmark/
    // attachment details - the id -> summary lookup happens here, the same way Notes'
    // own Details page resolves BookmarkIds/AttachmentIds to summaries.
    public List<BookmarkDto> AssociatedBookmarks { get; set; } = [];
    public List<AttachmentDto> AssociatedAttachments { get; set; } = [];

    private Dictionary<long, AttachmentTypeDto> _attachmentTypesById = [];

    // Null when the attachment has no AttachmentTypeId, or the type it referred to no longer
    // exists - same helper/semantics as Attachments' own Index page (AttachmentType(...)) so
    // the icon/row-color treatment stays identical wherever an attachment is listed. Used for
    // the Project's own AssociatedAttachments here; _ProjectNoteItem uses the shared
    // AttachmentTypesById lookup handed down via ProjectNoteItemViewModel instead.
    public AttachmentTypeDto? AttachmentType(long? attachmentTypeId) =>
        attachmentTypeId is long id ? _attachmentTypesById.GetValueOrDefault(id) : null;

    // Each associated Note rendered in full (title, description, body, its own bookmarks/
    // attachments) inside a collapsible container, ordered oldest-first (by DateCreated
    // ascending) per the display requirement for a Project - so a reader can go through them
    // in original context, one at a time, without leaving the page.
    public List<ProjectNoteItemViewModel> AssociatedNoteItems { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await projectsApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Project not found.");
            return RedirectToPage("Index");
        }

        Project = result.Value;

        var notes = new List<NoteDto>();
        if (Project.NoteIds.Count > 0)
        {
            var notesResult = await notesApiClient.GetListAsync(cancellationToken);
            if (notesResult.IsSuccess)
            {
                var noteIds = Project.NoteIds.ToHashSet();
                notes = (notesResult.Value ?? [])
                    .Where(note => noteIds.Contains(note.Id))
                    .OrderBy(note => note.DateCreated)
                    .ToList();
            }
        }

        // Bookmarks/attachments are needed both for the Project's own direct associations and
        // for each associated Note's own associations - fetched once here and shared via
        // lookups, rather than once per note.
        var needsBookmarks = Project.BookmarkIds.Count > 0 || notes.Any(note => note.BookmarkIds.Count > 0);
        var needsAttachments = Project.AttachmentIds.Count > 0 || notes.Any(note => note.AttachmentIds.Count > 0);

        var bookmarksById = new Dictionary<long, BookmarkDto>();
        var bookmarkTypesById = new Dictionary<long, BookmarkTypeDto>();
        if (needsBookmarks)
        {
            var bookmarksTask = bookmarksApiClient.GetListAsync(cancellationToken);
            var bookmarkTypesTask = bookmarkTypesApiClient.GetListAsync(cancellationToken);
            await Task.WhenAll(bookmarksTask, bookmarkTypesTask);

            var bookmarksResult = await bookmarksTask;
            if (bookmarksResult.IsSuccess)
            {
                bookmarksById = (bookmarksResult.Value ?? []).ToDictionary(bookmark => bookmark.Id);
            }

            var bookmarkTypesResult = await bookmarkTypesTask;
            if (bookmarkTypesResult.IsSuccess)
            {
                bookmarkTypesById = (bookmarkTypesResult.Value ?? []).ToDictionary(bookmarkType => bookmarkType.Id);
            }
        }

        var attachmentsById = new Dictionary<long, AttachmentDto>();
        if (needsAttachments)
        {
            var attachmentsTask = attachmentsApiClient.GetListAsync(cancellationToken);
            var attachmentTypesTask = attachmentTypesApiClient.GetListAsync(cancellationToken);
            await Task.WhenAll(attachmentsTask, attachmentTypesTask);

            var attachmentsResult = await attachmentsTask;
            if (attachmentsResult.IsSuccess)
            {
                attachmentsById = (attachmentsResult.Value ?? []).ToDictionary(attachment => attachment.Id);
            }

            var attachmentTypesResult = await attachmentTypesTask;
            if (attachmentTypesResult.IsSuccess)
            {
                _attachmentTypesById = (attachmentTypesResult.Value ?? []).ToDictionary(attachmentType => attachmentType.Id);
            }
        }

        AssociatedBookmarks = [.. Project.BookmarkIds.Select(bookmarksById.GetValueOrDefault).OfType<BookmarkDto>()];
        AssociatedAttachments = [.. Project.AttachmentIds.Select(attachmentsById.GetValueOrDefault).OfType<AttachmentDto>()];

        AssociatedNoteItems = notes
            .Select(note => new ProjectNoteItemViewModel
            {
                Note = note,
                AssociatedBookmarks = [.. note.BookmarkIds.Select(bookmarksById.GetValueOrDefault).OfType<BookmarkDto>()],
                AssociatedAttachments = [.. note.AttachmentIds.Select(attachmentsById.GetValueOrDefault).OfType<AttachmentDto>()],
                BookmarkTypesById = bookmarkTypesById,
                AttachmentTypesById = _attachmentTypesById
            })
            .ToList();

        return Page();
    }
}
