using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

// Mirrors ProjectA.Api's blog admin endpoints (/api/blog/posts|categories|settings|analytics).
public static class BlogPostType
{
    public const short Post = 1;
    public const short Page = 2;
}

public sealed class BlogPostListItemDto
{
    public long Id { get; set; }
    public short PostType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int PostViews { get; set; }
    public List<string> Categories { get; set; } = [];
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

public sealed class BlogPostDto
{
    public long Id { get; set; }
    public short PostType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int PostViews { get; set; }
    public long? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public List<string> Categories { get; set; } = [];
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

// Bound by both the Create and Edit pages. Categories are edited as one comma-separated text
// box (Blogifier's tag-style input) and split into the API's string list on the way out.
public sealed class BlogPostInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    // Format is validated by the API (only when the slug actually changes, so migrated
    // Blogifier slugs that predate today's rules stay editable).
    [StringLength(160)]
    [Display(Name = "Slug (leave blank to generate from the title)")]
    public string? Slug { get; set; }

    [StringLength(450)]
    [Display(Name = "Description (shown in post lists and search results)")]
    public string? Description { get; set; }

    [Display(Name = "Content (Markdown)")]
    public string? Content { get; set; }

    [StringLength(400)]
    [Display(Name = "Cover image path or URL")]
    public string? Cover { get; set; }

    [Display(Name = "Type")]
    public short PostType { get; set; } = BlogPostType.Post;

    [Display(Name = "Featured")]
    public bool IsFeatured { get; set; }

    [Display(Name = "Published")]
    public bool IsPublished { get; set; }

    [Display(Name = "Categories (comma separated)")]
    public string? CategoriesText { get; set; }

    public IReadOnlyList<string> CategoryList =>
        (CategoriesText ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    public static BlogPostInput From(BlogPostDto post) => new()
    {
        Title = post.Title,
        Slug = post.Slug,
        Description = post.Description,
        Content = post.Content,
        Cover = post.Cover,
        PostType = post.PostType,
        IsFeatured = post.IsFeatured,
        IsPublished = post.PublishedAt is not null,
        CategoriesText = string.Join(", ", post.Categories)
    };
}

// Wire shape of the API's Create/UpdateBlogPostRequest.
public sealed record BlogPostRequest(
    string Title,
    string? Slug,
    string? Description,
    string? Content,
    string? Cover,
    short PostType,
    bool IsFeatured,
    bool IsPublished,
    IReadOnlyList<string> Categories)
{
    public static BlogPostRequest From(BlogPostInput input) => new(
        input.Title, input.Slug, input.Description, input.Content, input.Cover,
        input.PostType, input.IsFeatured, input.IsPublished, input.CategoryList);
}

public sealed class BlogCategoryDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PostCount { get; set; }
}

public sealed class BlogCategoryInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }
}

public sealed class BlogSettingsDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ItemsPerPage { get; set; } = 10;
    public bool IncludeFeatured { get; set; }
    public string? Cover { get; set; }
    public string? Logo { get; set; }
    public string? HeaderScript { get; set; }
    public string? FooterScript { get; set; }
}

public sealed class BlogSettingsInput
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [StringLength(450)]
    public string? Description { get; set; }

    [Range(1, 100)]
    [Display(Name = "Posts per page")]
    public int ItemsPerPage { get; set; } = 10;

    [Display(Name = "Include featured posts in the main post list")]
    public bool IncludeFeatured { get; set; }

    [StringLength(400)]
    [Display(Name = "Default cover image")]
    public string? Cover { get; set; }

    [StringLength(400)]
    public string? Logo { get; set; }

    [Display(Name = "Header script (injected into <head> on every page)")]
    public string? HeaderScript { get; set; }

    [Display(Name = "Footer script (injected before </body> on every page)")]
    public string? FooterScript { get; set; }
}

public sealed class BlogAnalyticsDto
{
    public int TotalPosts { get; set; }
    public int TotalPages { get; set; }
    public int TotalDrafts { get; set; }
    public int TotalViews { get; set; }
    public int Latest { get; set; }
    public List<BlogPostViewsDto> LatestPostViews { get; set; } = [];
}

public sealed class BlogPostViewsDto
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Views { get; set; }
    public DateTime PublishedAt { get; set; }
}

// GET /api/blog/analytics/daily - views per UTC day for one post/page (PostId) or the whole blog.
public sealed class BlogDailyViewsDto
{
    public long? PostId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int TotalViews { get; set; }
    public List<BlogDayViewsDto> Days { get; set; } = [];
    public List<BlogPostViewTotalDto> Posts { get; set; } = [];
}

public sealed class BlogDayViewsDto
{
    public DateOnly Day { get; set; }
    public int Views { get; set; }
}

public sealed class BlogPostViewTotalDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public short PostType { get; set; }
    public int Views { get; set; }
}

// GET /api/blog/analytics/daily/{day} - one day's views grouped by IP address + user agent.
public sealed class BlogDailyVisitorsDto
{
    public long? PostId { get; set; }
    public DateOnly Day { get; set; }
    public int TotalViews { get; set; }
    public int TotalVisitors { get; set; }
    public List<BlogVisitorDto> Visitors { get; set; } = [];
}

public sealed class BlogVisitorDto
{
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public int Views { get; set; }
    public DateTime FirstViewedAt { get; set; }
    public DateTime LastViewedAt { get; set; }
    public string Posts { get; set; } = string.Empty;
}

public sealed class UserProfileDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? Avatar { get; set; }
}

public sealed class UserProfileInput
{
    [StringLength(160)]
    [Display(Name = "Display name (shown as the author on blog posts)")]
    public string? DisplayName { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }

    [StringLength(400)]
    [Display(Name = "Avatar image path or URL")]
    public string? Avatar { get; set; }
}
