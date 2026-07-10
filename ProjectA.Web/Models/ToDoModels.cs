using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class ToDoDto
{
    public long Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public bool Done { get; set; }
}

// Bound by the Create page - CreateToDoRequest has no Done flag (new items always start
// outstanding).
public sealed class ToDoCreateInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    // Free text, not a CategoryId - ProjectA.Api's ToDo endpoints still use the original
    // `category` text column, not the (currently unused) category_id foreign key.
    [Required(ErrorMessage = "Category is required.")]
    [StringLength(128)]
    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }
}

// Bound by the Edit page - UpdateToDoRequest additionally carries the completion state.
public sealed class ToDoEditInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [StringLength(128)]
    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Display(Name = "Done")]
    public bool Done { get; set; }
}
