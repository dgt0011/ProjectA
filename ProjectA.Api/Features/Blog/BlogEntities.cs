using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.Blog;

// Map 1:1 onto the blog_* tables (script014) for Dapper.Contrib. Property names must match
// column names exactly - Dapper.Contrib emits them verbatim and Postgres folds unquoted
// identifiers to lowercase, so these stay snake_case rather than PascalCase.
// ReSharper disable InconsistentNaming
[Table("blog_posts")]
internal sealed class BlogPostDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public long? author_id { get; set; }
    public short post_type { get; set; } = BlogPostTypes.Post;
    public string title { get; set; } = string.Empty;
    public string slug { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
    public string? cover { get; set; }
    public int post_views { get; set; }
    public bool is_featured { get; set; }
    // NULL = draft. Blogifier used DateTime.MinValue for this; the data-copy script maps that
    // sentinel to NULL.
    public DateTime? published_at { get; set; }
    public DateTime date_created { get; set; }
    public DateTime? date_modified { get; set; }
}

[Table("blog_categories")]
internal sealed class BlogCategoryDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string title { get; set; } = string.Empty;
    public string? description { get; set; }
    public DateTime date_created { get; set; }
    public DateTime? date_modified { get; set; }
}

[Table("blog_settings")]
internal sealed class BlogSettingsDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string title { get; set; } = string.Empty;
    public string? description { get; set; }
    public int items_per_page { get; set; } = 10;
    public bool include_featured { get; set; } = true;
    public string? cover { get; set; }
    public string? logo { get; set; }
    public string? header_script { get; set; }
    public string? footer_script { get; set; }
    public DateTime date_created { get; set; }
    public DateTime? date_modified { get; set; }
}
// ReSharper restore InconsistentNaming

// Same numeric values Blogifier stored in Posts.PostType, so migrated rows keep their meaning.
public static class BlogPostTypes
{
    public const short Post = 1;
    public const short Page = 2;

    public static bool IsValid(short value) => value is Post or Page;
}
