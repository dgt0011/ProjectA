using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class ProjectDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
}

// Bound by both Create and Edit pages - CreateProjectRequest and UpdateProjectRequest share
// the same (Title, Description, StartDate) shape. StartDate is a DateOnly here purely for a
// clean <input type="date"> on the form; ProjectsApiClient converts it to a UTC-kind DateTime
// before it goes on the wire, since the API stores it in a `timestamptz` column.
public sealed class ProjectInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    [Display(Name = "Start date")]
    public DateOnly? StartDate { get; set; }
}
