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
    IAttachmentsApiClient attachmentsApiClient,
    IProjectsApiClient projectsApiClient) : PageModel
{
    // Set when this page is reached via the "Create note" button on a Project's Details page
    // (asp-route-projectId) - carried through the form as a hidden field so it survives the
    // POST too. When present, the new note is associated with that project on save and the
    // user is sent back to the Project's Details page instead of the Notes list.
    [BindProperty(SupportsGet = true)]
    public long? ProjectId { get; set; }

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

        if (ProjectId is { } projectId)
        {
            await AssociateWithProjectAsync(projectId, result.Value!.Id, cancellationToken);

            TempData["SuccessMessage"] = "Note created.";
            return RedirectToPage("/Projects/Details", new { id = projectId });
        }

        TempData["SuccessMessage"] = "Note created.";
        return RedirectToPage("Index");
    }

    // Adds the newly created note to the project's NoteIds - UpdateProjectEndpoint replaces
    // the whole set on every call (null means unchanged, but an explicit list always
    // overwrites), so this has to send every existing NoteId back too, not just the new one.
    // If the project can no longer be loaded (deleted moments ago, API hiccup, etc.) this
    // silently leaves the note un-associated rather than failing the whole request - the note
    // itself was already created successfully by this point.
    private async Task AssociateWithProjectAsync(long projectId, long noteId, CancellationToken cancellationToken)
    {
        var projectResult = await projectsApiClient.GetByIdAsync(projectId, cancellationToken);
        if (!projectResult.IsSuccess || projectResult.Value is null)
        {
            return;
        }

        var project = projectResult.Value;
        var input = new ProjectInput
        {
            Title = project.Title,
            Description = project.Description,
            StartDate = DateOnly.FromDateTime(project.StartDate),
            IsPrivate = project.IsPrivate,
            NoteIds = [.. project.NoteIds.Append(noteId).Distinct()],
            BookmarkIds = [.. project.BookmarkIds],
            AttachmentIds = [.. project.AttachmentIds]
        };

        await projectsApiClient.UpdateAsync(projectId, input, cancellationToken);
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
