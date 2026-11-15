namespace ProjectA.Api.Features.Blog.Posts;

// Create and Update accept the same editable fields, so their validation rules are shared
// here rather than duplicated (field limits mirror the blog_posts column sizes).
internal static class BlogPostValidation
{
    public static Dictionary<string, string[]> Validate(
        string? title, string? slug, string? description, string? cover, short postType, IEnumerable<string>? categories)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["Title"] = ["Title is required."];
        }
        else if (title.Trim().Length > 160)
        {
            errors["Title"] = ["Title must be 160 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(slug) && !BlogSlugs.IsValid(slug.Trim()))
        {
            errors["Slug"] = ["Slug may only contain letters, numbers, '-' and '_' (max 160 characters)."];
        }

        if (description is { Length: > 450 })
        {
            errors["Description"] = ["Description must be 450 characters or fewer."];
        }

        if (cover is { Length: > 400 })
        {
            errors["Cover"] = ["Cover must be 400 characters or fewer."];
        }

        if (!BlogPostTypes.IsValid(postType))
        {
            errors["PostType"] = ["PostType must be 1 (post) or 2 (page)."];
        }

        if (BlogPostCategoryLinks.Normalize(categories).Any(c => c.Length > BlogPostCategoryLinks.MaxTitleLength))
        {
            errors["Categories"] = [$"Category names must be {BlogPostCategoryLinks.MaxTitleLength} characters or fewer."];
        }

        return errors;
    }

    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
