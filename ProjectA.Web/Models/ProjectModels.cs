using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class ProjectDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
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

    [Display(Name = "Notes")]
    public List<long> NoteIds { get; set; } = [];

    [Display(Name = "Bookmarks")]
    public List<long> BookmarkIds { get; set; } = [];

    [Display(Name = "Attachments")]
    public List<long> AttachmentIds { get; set; } = [];
}
