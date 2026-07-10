using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class AttachmentDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string S3Arn { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

// Bound by both Create and Edit pages - CreateAttachmentRequest and UpdateAttachmentRequest
// share the same (Title, Description, S3Arn) shape.
public sealed class AttachmentInput
{
    [StringLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = "S3 ARN is required.")]
    [Display(Name = "S3 ARN")]
    public string S3Arn { get; set; } = string.Empty;
}
