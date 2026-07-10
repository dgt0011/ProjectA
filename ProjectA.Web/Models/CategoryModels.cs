using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

// Matches the shape returned by every Categories endpoint (list item, get-by-id, and the
// create/update response) - ProjectA.Api uses the same CategoryResponse-shaped record
// everywhere for this entity.
public sealed class CategoryDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// Bound by both Create and Edit pages - CreateCategoryRequest and UpdateCategoryRequest on
// the API side have the identical (Title, Description) shape.
public sealed class CategoryInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
}
