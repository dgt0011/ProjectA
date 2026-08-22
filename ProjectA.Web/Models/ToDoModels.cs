using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class ToDoDto
{
    public long Id { get; set; }
    public long? CategoryId { get; set; }
    public long? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CompletionNotes { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public bool Done { get; set; }
}

// Bound by the Create page - CreateToDoRequest has no Done flag (new items always start
// outstanding). Also reused as-is by Projects/Details.cshtml.cs's "Create ToDo" modal (same
// Title/CategoryId/Description fields), which sets ProjectId itself from the route before
// calling IToDoApiClient.CreateAsync.
public sealed class ToDoCreateInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    // Optional - a ToDo left without a category is grouped as "Uncategorized" wherever ToDo
    // lists are displayed (ahead of the categorized groups), rather than being required here.
    [Display(Name = "Category (optional)")]
    public long? CategoryId { get; set; }

    public string? Description { get; set; }

    public long? ProjectId { get; set; }
}

// Bound by the Edit page - UpdateToDoRequest additionally carries the completion state.
public sealed class ToDoEditInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    // Optional - see ToDoCreateInput.CategoryId.
    [Display(Name = "Category (optional)")]
    public long? CategoryId { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Done")]
    public bool Done { get; set; }

    public long? ProjectId { get; set; }
}

// Bound by the Project Details page's "Complete" modal.
public sealed class ToDoCompleteInput
{
    [Display(Name = "Completion notes")]
    public string? Notes { get; set; }
}
