using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog.Posts;

// Lists posts or pages (the ?type= route value) with Blogifier's All/Published/Drafts/Featured
// filter and its one-click publish/feature/delete actions.
[Authorize]
public class IndexModel(IBlogPostsApiClient blogPostsApiClient, IConfiguration configuration) : PageModel
{
    public static readonly string[] Statuses = ["all", "published", "drafts", "featured"];

    [BindProperty(SupportsGet = true)]
    public short Type { get; set; } = BlogPostType.Post;

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string? Term { get; set; }

    public List<BlogPostListItemDto> Posts { get; private set; } = [];
    public bool LoadedSuccessfully { get; private set; } = true;

    public bool IsPages => Type == BlogPostType.Page;
    public string Noun => IsPages ? "page" : "post";
    public string? BlogSiteUrl => configuration["BlogSite:BaseUrl"]?.TrimEnd('/');

    public string PublicUrl(BlogPostListItemDto post) =>
        $"{BlogSiteUrl}/{(post.PostType == BlogPostType.Page ? "" : "posts/")}{post.Slug}";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Normalize();
        var result = await blogPostsApiClient.GetListAsync(Status, Type, Term, cancellationToken);
        if (result.IsSuccess)
        {
            Posts = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }
    }

    public Task<IActionResult> OnPostPublishAsync(long id, bool value, CancellationToken cancellationToken) =>
        RunAsync(() => blogPostsApiClient.SetPublishedAsync(id, value, cancellationToken),
            value ? $"The {Noun} is now published." : $"The {Noun} is now a draft.");

    public Task<IActionResult> OnPostFeatureAsync(long id, bool value, CancellationToken cancellationToken) =>
        RunAsync(() => blogPostsApiClient.SetFeaturedAsync(id, value, cancellationToken),
            value ? $"The {Noun} is now featured." : $"The {Noun} is no longer featured.");

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await blogPostsApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? $"The {Noun} was deleted."
            : result.ToDisplayMessage($"Could not delete the {Noun}.");
        return RedirectToSelf();
    }

    private async Task<IActionResult> RunAsync(Func<Task<Common.ApiResult<object>>> action, string successMessage)
    {
        var result = await action();
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? successMessage
            : result.ToDisplayMessage($"Could not update the {Noun}.");
        return RedirectToSelf();
    }

    private IActionResult RedirectToSelf()
    {
        Normalize();
        return RedirectToPage(new { type = Type, status = Status, term = Term });
    }

    private void Normalize()
    {
        if (Type != BlogPostType.Page)
        {
            Type = BlogPostType.Post;
        }

        if (!Statuses.Contains(Status))
        {
            Status = "all";
        }
    }
}
