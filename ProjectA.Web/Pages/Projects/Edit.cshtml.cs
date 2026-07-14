using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Projects;

[Authorize]
public class EditModel(
    IProjectsApiClient projectsApiClient,
    INotesApiClient notesApiClient,
    IBookmarksApiClient bookmarksApiClient,
    IAttachmentsApiClient attachmentsApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public ProjectInput Form { get; set; } = new();

    public List<NoteDto> AvailableNotes { get; set; } = [];
    public List<BookmarkDto> AvailableBookmarks { get; set; } = [];
    public List<AttachmentDto> AvailableAttachments { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await projectsApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Project not found.");
            return RedirectToPage("Index");
        }

        Form = new ProjectInput
        {
            Title = result.Value.Title,
            Description = result.Value.Description,
            StartDate = DateOnly.FromDateTime(result.Value.StartDate),
            NoteIds = [.. result.Value.NoteIds],
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

        var result = await projectsApiClient.UpdateAsync(Id, Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not update the project."));
            }

            await LoadAssociationOptionsAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = "Project updated.";
        return RedirectToPage("Index");
    }

    private async Task LoadAssociationOptionsAsync(CancellationToken cancellationToken)
    {
        var notesTask = notesApiClient.GetListAsync(cancellationToken);
        var bookmarksTask = bookmarksApiClient.GetListAsync(cancellationToken);
        var attachmentsTask = attachmentsApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(notesTask, bookmarksTask, attachmentsTask);

        var notesResult = await notesTask;
        if (notesResult.IsSuccess)
        {
            AvailableNotes = notesResult.Value ?? [];
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
