using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class NoteDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Body { get; set; }
    public long? ParentNoteId { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public List<long> BookmarkIds { get; set; } = [];
    public List<long> AttachmentIds { get; set; } = [];
    public List<long> CategoryIds { get; set; } = [];
}

// Bound by both Create and Edit pages - CreateNoteRequest and UpdateNoteRequest share the
// same (Title, Description, Body, BookmarkIds, AttachmentIds, CategoryIds) shape. Neither Title nor Body
// is individually [Required]: ProjectA.Api only rejects the request if *both* are blank, so
// IValidatableObject below mirrors that same cross-field rule client-side instead of always
// requiring both. Description is always optional - it's just a short summary shown in the
// Notes list. B
// ookmarkIds/AttachmentIds/CategoryIds always reflect exactly what's selected in the
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

    // Only logged-in users ever see this checkbox (Create/Edit are both [Authorize]-gated),
    // but the effect reaches anonymous visitors: a private note - and any child note that
    // doesn't set its own flag - is hidden from them everywhere (Notes list, direct links,
    // and any Project it's associated with). Enforced API-side, not just hidden client-side.
    [Display(Name = "Private")]
    public bool IsPrivate { get; set; }

    [Display(Name = "Bookmarks")]
    public List<long> BookmarkIds { get; set; } = [];

    [Display(Name = "Attachments")]
    public List<long> AttachmentIds { get; set; } = [];
    
    [Display(Name = "Categories")]
    public List<long> CategoryIds { get; set; } = [];

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

    // Null on the standalone Notes/Details page (its own child-tree display is unchanged - no
    // Edit button there). Set by Projects/_ProjectNoteItem to that Project's own page URL, so
    // every Project Note's child notes get an auth-gated "Edit" link too, returning to the
    // Project page on save - propagated unchanged to every deeper level of the same tree.
    public string? ReturnUrl { get; init; }
}
