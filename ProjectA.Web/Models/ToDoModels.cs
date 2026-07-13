using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class ToDoDto
{
    public long Id { get; set; }
    public long? CategoryId { get; set; }
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

    [Required(ErrorMessage = "Category is required.")]
    [Display(Name = "Category")]
    public long? CategoryId { get; set; }

    public string? Description { get; set; }
}

// Bound by the Edit page - UpdateToDoRequest additionally carries the completion state.
public sealed class ToDoEditInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [Display(Name = "Category")]
    public long? CategoryId { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Done")]
    public bool Done { get; set; }
}
