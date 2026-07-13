using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class BookmarkDto
{
    public long Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int Rating { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public List<long> CategoryIds { get; set; } = [];
}

// Bound by both Create and Edit pages - CreateBookmarkRequest and UpdateBookmarkRequest
// share the same (Url, Title, Description, Rating, CategoryIds) shape. CategoryIds always
// reflects exactly the categories selected in the multi-select list, so it is sent as an
// explicit list (never null/omitted) - this page never needs the API's "omit CategoryIds to
// leave associations unchanged" behaviour, since the select always shows the current state.
public sealed class BookmarkInput
{
    [Required(ErrorMessage = "Url is required.")]
    [Display(Name = "URL")]
    public string Url { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [Range(1, 10, ErrorMessage = "Rating must be between 1 and 10.")]
    public int Rating { get; set; } = 1;

    [Display(Name = "Categories")]
    public List<long> CategoryIds { get; set; } = [];
}
