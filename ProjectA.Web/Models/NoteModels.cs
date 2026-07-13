using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class NoteDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Body { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

// Bound by both Create and Edit pages - CreateNoteRequest and UpdateNoteRequest share the
// same (Title, Description, Body) shape. Neither Title nor Body is individually [Required]:
// ProjectA.Api only rejects the request if *both* are blank, so IValidatableObject below
// mirrors that same cross-field rule client-side instead of always requiring both.
// Description is always optional - it's just a short summary shown in the Notes list.
public sealed class NoteInput : IValidatableObject
{
    public string? Title { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public string? Body { get; set; }

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
