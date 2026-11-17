namespace ProjectA.Blog.Web.Models;

// Mirrors the anonymous /api/blog endpoints in ProjectA.Api (Features/Blog/Public).
public static class PostType
{
    public const short Post = 1;
    public const short Page = 2;
}

public sealed class BlogSite
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ItemsPerPage { get; set; } = 10;
    public bool IncludeFeatured { get; set; }
    public string? Cover { get; set; }
    public string? Logo { get; set; }
    public string? HeaderScript { get; set; }
    public string? FooterScript { get; set; }
    public List<BlogSiteCategory> Categories { get; set; } = [];

    // Shown when the API is unreachable, so the layout can still render an error page.
    public static BlogSite Unavailable { get; } = new() { Title = "Blog" };
}

public sealed class BlogSiteCategory
{
    public string Title { get; set; } = string.Empty;
    public int PostCount { get; set; }
}

public sealed class CategoryRef
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public sealed class PostSummary
{
    public long Id { get; set; }
    public short PostType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public DateTime PublishedAt { get; set; }
    public bool IsFeatured { get; set; }
    public int PostViews { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public List<CategoryRef> Categories { get; set; } = [];
}

public sealed class PostPage
{
    public List<PostSummary> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int ItemsPerPage { get; set; }
    public int TotalItems { get; set; }
    public bool HasOlder { get; set; }
    public bool HasNewer { get; set; }

    public static PostPage Empty { get; } = new();
}

public sealed class PostLink
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class PostDetail
{
    public PostSummary Post { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public string? AuthorBio { get; set; }
    public PostLink? Older { get; set; }
    public PostLink? Newer { get; set; }
    public List<PostSummary> Related { get; set; } = [];
}

public sealed class FeedEntry
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = [];
}

public sealed class SitemapEntry
{
    public string Slug { get; set; } = string.Empty;
    public short PostType { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime LastModified { get; set; }
}
