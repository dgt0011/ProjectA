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
    IAttachmentsApiClient attachmentsApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public ProjectDto? Project { get; set; }

    // GetProjectByIdEndpoint only returns the associated ids, not full note/bookmark/
    // attachment details - the id -> summary lookup happens here, the same way Notes'
    // own Details page resolves BookmarkIds/AttachmentIds to summaries. Notes are ordered
    // oldest-first (by DateCreated ascending) per the display requirement for a Project.
    public List<NoteDto> AssociatedNotes { get; set; } = [];
    public List<BookmarkDto> AssociatedBookmarks { get; set; } = [];
    public List<AttachmentDto> AssociatedAttachments { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await projectsApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Project not found.");
            return RedirectToPage("Index");
        }

        Project = result.Value;

        if (Project.NoteIds.Count > 0)
        {
            var notesResult = await notesApiClient.GetListAsync(cancellationToken);
            if (notesResult.IsSuccess)
            {
                var noteIds = Project.NoteIds.ToHashSet();
                AssociatedNotes = (notesResult.Value ?? [])
                    .Where(note => noteIds.Contains(note.Id))
                    .OrderBy(note => note.DateCreated)
                    .ToList();
            }
        }

        if (Project.BookmarkIds.Count > 0)
        {
            var bookmarksResult = await bookmarksApiClient.GetListAsync(cancellationToken);
            if (bookmarksResult.IsSuccess)
            {
                var bookmarkIds = Project.BookmarkIds.ToHashSet();
                AssociatedBookmarks = (bookmarksResult.Value ?? [])
                    .Where(bookmark => bookmarkIds.Contains(bookmark.Id))
                    .ToList();
            }
        }

        if (Project.AttachmentIds.Count > 0)
        {
            var attachmentsResult = await attachmentsApiClient.GetListAsync(cancellationToken);
            if (attachmentsResult.IsSuccess)
            {
                var attachmentIds = Project.AttachmentIds.ToHashSet();
                AssociatedAttachments = (attachmentsResult.Value ?? [])
                    .Where(attachment => attachmentIds.Contains(attachment.Id))
                    .ToList();
            }
        }

        return Page();
    }
}
