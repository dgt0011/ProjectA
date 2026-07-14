using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

// Matches the shape returned by every BookmarkTypes endpoint (list item, get-by-id, and the
// create/update response). The icon itself is never inlined here (it would bloat every list/get
// call) - pages that need to display it point an <img> at the Web app's own icon-proxy route,
// keyed off Id, and only bother doing so when HasIcon is true.
public sealed class BookmarkTypeDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool HasIcon { get; set; }
}

// Bound by both Create and Edit pages. IconBase64/IconContentType are populated by the page
// handler from an uploaded IFormFile (see BookmarkTypes/Create.cshtml.cs) rather than bound
// directly from the form - asp-for can't bind an <input type="file"> to a string. RemoveIcon
// only makes sense on Edit (there's nothing to remove yet on Create) but lives here too so both
// pages can share one input model.
public sealed class BookmarkTypeInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Color")]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a hex value like #1A2B3C.")]
    public string? Color { get; set; }

    public string? IconBase64 { get; set; }
    public string? IconContentType { get; set; }

    [Display(Name = "Remove icon")]
    public bool RemoveIcon { get; set; }
}
