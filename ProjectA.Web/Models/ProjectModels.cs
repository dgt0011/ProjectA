using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class ProjectDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsPrivate { get; set; }
    public List<long> NoteIds { get; set; } = [];
    public List<long> BookmarkIds { get; set; } = [];
    public List<long> AttachmentIds { get; set; } = [];
}

// Bound by both Create and Edit pages - CreateProjectRequest and UpdateProjectRequest share
// the same (Title, Description, StartDate, NoteIds, BookmarkIds, AttachmentIds) shape.
// StartDate is a DateOnly here purely for a clean <input type="date"> on the form;
// ProjectsApiClient converts it to a UTC-kind DateTime before it goes on the wire, since the
// API stores it in a `timestamptz` column. NoteIds/BookmarkIds/AttachmentIds always reflect
// exactly what's selected in the multi-select lists, so they are sent as explicit lists
// (never null/omitted) - this page never needs the API's "omit to leave associations
// unchanged" behaviour (mirrors NoteInput's BookmarkIds/AttachmentIds).
public sealed class ProjectInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    [Display(Name = "Start date")]
    public DateOnly? StartDate { get; set; }

    // Only logged-in users ever see this checkbox (Create/Edit are both [Authorize]-gated),
    // but the effect reaches anonymous visitors: a private project is hidden from them
    // everywhere (Projects list, direct links). Enforced API-side, not just hidden
    // client-side.
    [Display(Name = "Private")]
    public bool IsPrivate { get; set; }

    [Display(Name = "Notes")]
    public List<long> NoteIds { get; set; } = [];

    [Display(Name = "Bookmarks")]
    public List<long> BookmarkIds { get; set; } = [];

    [Display(Name = "Attachments")]
    public List<long> AttachmentIds { get; set; } = [];
}

// View model for rendering one Note in full (title, description, body, its own bookmarks/
// attachments) inside a collapsible container on the Project Details page. BookmarkTypesById
// is the same shared lookup for every note on the page (built once in DetailsModel), handed
// down so the _ProjectNoteItem partial can resolve each bookmark's icon/color without its own
// round trip.
public sealed class ProjectNoteItemViewModel
{
    public required NoteDto Note { get; init; }

    public required List<BookmarkDto> AssociatedBookmarks { get; init; }

    public required List<AttachmentDto> AssociatedAttachments { get; init; }

    public required IReadOnlyDictionary<long, BookmarkTypeDto> BookmarkTypesById { get; init; }

    // Same shared-lookup reasoning as BookmarkTypesById, mirrored for Attachments.
    public required IReadOnlyDictionary<long, AttachmentTypeDto> AttachmentTypesById { get; init; }

    // Built once (from the full, unfiltered notes list) in DetailsModel and handed down
    // unchanged to every top-level Note on the page, the same shared-lookup pattern as
    // BookmarkTypesById/AttachmentTypesById - a Note's children aren't necessarily themselves
    // associated with this Project, so this can't be derived from AssociatedNoteItems alone.
    // Passed straight through to Notes' own _NoteTreeItem partial (via NoteTreeItemViewModel)
    // to render this Note's child hierarchy, expanded, the same way Notes/Details.cshtml does.
    public required IReadOnlyDictionary<long, List<NoteDto>> ChildrenByParentId { get; init; }

    // This Project's own Details page URL, precomputed once in DetailsModel (every Note on the
    // page shares the same value) - handed to each Note's "Edit" link as ?returnUrl=... so
    // Notes/Edit sends the user back here after saving instead of to the Notes list.
    public required string ReturnUrl { get; init; }
}
