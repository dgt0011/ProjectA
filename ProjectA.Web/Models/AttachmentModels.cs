using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class AttachmentDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }

    // A web-relative path under wwwroot (e.g. "/files/3f2c1a9e-report.pdf") - files are saved
    // locally by ProjectA.Web (see AttachmentFileStorage), not in AWS S3 despite the historical
    // "S3Arn" name this replaced everywhere else in the stack.
    public string FilePath { get; set; } = string.Empty;
    public long? AttachmentTypeId { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

// Bound by both Create and Edit pages - CreateAttachmentRequest and UpdateAttachmentRequest
// share the same (Title, Description, FilePath) shape. On the Create page FilePath isn't
// typed in directly - it's populated from an uploaded file (see Attachments/Create.cshtml.cs)
// before this is ever sent to the API, so [Required] here is only actually enforced against
// user input on the Edit page.
public sealed class AttachmentInput
{
    [StringLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = "File path is required.")]
    [Display(Name = "File path")]
    public string FilePath { get; set; } = string.Empty;

    [Display(Name = "Attachment Type")]
    public long? AttachmentTypeId { get; set; }
}
