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
        else if (!string.IsNullOrWhiteSpace(slug) && BlogSlugs.IsReserved(slug.Trim()))
        {
            errors["Slug"] = [$"'{slug.Trim()}' is reserved by the blog site's own pages; choose another slug."];
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

        if (categories?.Any(c => c is null) == true)
        {
            errors["Categories"] = ["Category names cannot be null."];
        }
        else if (BlogPostCategoryLinks.Normalize(categories).FirstOrDefault(c => BlogPostCategoryLinks.ValidateTitle(c) is not null) is { } bad)
        {
            errors["Categories"] = [BlogPostCategoryLinks.ValidateTitle(bad)!];
        }

        return errors;
    }

    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
