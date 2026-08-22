using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Projects;

// Deliberately not [Authorize] - anyone can view a project's full content (matching
// GetProjectByIdEndpoint, which is anonymous on the API side too). Only the Edit link, the
// "Create note"/"Create ToDo" buttons, and the ToDo "Complete" button are gated on the viewer
// being signed in - each of the new POST handlers below checks that itself (Challenge() if not),
// the same per-handler pattern ToDo/Index.cshtml.cs's OnPostDeleteAsync already uses, since the
// whole PageModel can't be [Authorize] without breaking anonymous viewing.
public class DetailsModel(
    IProjectsApiClient projectsApiClient,
    INotesApiClient notesApiClient,
    IBookmarksApiClient bookmarksApiClient,
    IAttachmentsApiClient attachmentsApiClient,
    IBookmarkTypesApiClient bookmarkTypesApiClient,
    IAttachmentTypesApiClient attachmentTypesApiClient,
    IToDoApiClient toDoApiClient,
    ICategoriesApiClient categoriesApiClient) : PageModel
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

    // ToDos associated with this project - unlike Notes/Bookmarks/Attachments, todos.project_id
    // is a plain nullable FK column (one-to-many from the project's side, not a join table), so
    // this is a direct filtered list call rather than an id-lookup. Outstanding items first,
    // then completed, each group oldest-first - both shown together by default per this
    // section's display requirement (there's no hide-completed toggle here, unlike the
    // standalone ToDo Index page's includeDone checkbox).
    public List<ToDoDto> ProjectToDos { get; set; } = [];

    // For the "Create ToDo" modal's category dropdown - same field, same requirement, as the
    // standalone ToDo Create page, just reached without leaving this page.
    public List<CategoryDto> AvailableCategories { get; set; } = [];

    [BindProperty]
    public ToDoCreateInput CreateTodoForm { get; set; } = new();

    [BindProperty]
    public ToDoCompleteInput CompleteTodoForm { get; set; } = new();

    // Set on a failed Create-ToDo submit so the page can re-open that modal (rather than just
    // silently closing it) with its validation messages visible - see reopen-create-todo-modal
    // in Details.cshtml/_ProjectTodoScripts.
    public bool ShowCreateTodoModal { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateTodoAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        // The modal doesn't expose a Project picker - this page's own Id is the only project a
        // ToDo created here can ever be associated with.
        CreateTodoForm.ProjectId = Id;

        if (!ModelState.IsValid)
        {
            var invalidResult = await LoadAsync(cancellationToken);
            ShowCreateTodoModal = true;
            return invalidResult;
        }

        var result = await toDoApiClient.CreateAsync(CreateTodoForm, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors, prefix: "CreateTodoForm");
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not create the ToDo item."));
            }

            var failureResult = await LoadAsync(cancellationToken);
            ShowCreateTodoModal = true;
            return failureResult;
        }

        TempData["SuccessMessage"] = "ToDo item created.";
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostCompleteTodoAsync(long todoId, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var result = await toDoApiClient.CompleteAsync(todoId, CompleteTodoForm, cancellationToken);

        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "ToDo item completed."
            : result.ToDisplayMessage("Could not complete the ToDo item.");

        // Always redirect (rather than redisplaying the modal on failure) - the only field in
        // this modal is free-text notes, so there's no ModelState validation failure to show;
        // an API-level failure just surfaces as the error banner on the reloaded page.
        return RedirectToPage(new { Id });
    }

    private async Task<IActionResult> LoadAsync(CancellationToken cancellationToken)
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

        var todosTask = toDoApiClient.GetListAsync(includeDone: true, projectId: Id, cancellationToken: cancellationToken);
        var categoriesTask = categoriesApiClient.GetListAsync(cancellationToken);
        await Task.WhenAll(todosTask, categoriesTask);

        var todosResult = await todosTask;
        if (todosResult.IsSuccess)
        {
            ProjectToDos = (todosResult.Value ?? [])
                .OrderBy(todo => todo.Done)
                .ThenBy(todo => todo.DateCreated)
                .ToList();
        }

        var categoriesResult = await categoriesTask;
        if (categoriesResult.IsSuccess)
        {
            AvailableCategories = categoriesResult.Value ?? [];
        }

        return Page();
    }
}
