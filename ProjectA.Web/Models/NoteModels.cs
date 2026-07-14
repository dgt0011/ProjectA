using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class NoteDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Body { get; set; }
    public long? ParentNoteId { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public List<long> BookmarkIds { get; set; } = [];
    public List<long> AttachmentIds { get; set; } = [];
}

// Bound by both Create and Edit pages - CreateNoteRequest and UpdateNoteRequest share the
// same (Title, Description, Body, BookmarkIds, AttachmentIds) shape. Neither Title nor Body
// is individually [Required]: ProjectA.Api only rejects the request if *both* are blank, so
// IValidatableObject below mirrors that same cross-field rule client-side instead of always
// requiring both. Description is always optional - it's just a short summary shown in the
// Notes list. BookmarkIds/AttachmentIds always reflect exactly what's selected in the
// multi-select lists, so they are sent as explicit lists (never null/omitted) - this page
// never needs the API's "omit to leave associations unchanged" behaviour.
public sealed class NoteInput : IValidatableObject
{
    public string? Title { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public string? Body { get; set; }

    [Display(Name = "Parent note")]
    public long? ParentNoteId { get; set; }

    [Display(Name = "Bookmarks")]
    public List<long> BookmarkIds { get; set; } = [];

    [Display(Name = "Attachments")]
    public List<long> AttachmentIds { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Body))
        {
            const string message = "Either Title or Body is required.";
            yield return new ValidationResult(message, [nameof(Title)]);
            yield return new ValidationResult(message, [nameof(Body)]);
        }
    }
}

// View model for rendering one node of a Note's child hierarchy on the Details page. The
// same ChildrenByParentId lookup (built once, up front, from the full notes list) is handed
// down unchanged at every recursion depth so the _NoteTreeItem partial can look up its own
// children without needing to re-fetch or re-filter anything.
public sealed class NoteTreeItemViewModel
{
    public required NoteDto Note { get; init; }

    public required IReadOnlyDictionary<long, List<NoteDto>> ChildrenByParentId { get; init; }

    public int Depth { get; init; }
}
