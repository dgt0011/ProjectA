using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog.Posts;

[Authorize]
public class EditModel(
    IBlogPostsApiClient blogPostsApiClient,
    IBlogCategoriesApiClient blogCategoriesApiClient,
    BlogFileStorage blogFileStorage,
    IConfiguration configuration) : BlogPostFormPageModel(blogCategoriesApiClient, blogFileStorage)
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    public BlogPostDto? Existing { get; private set; }

    public string? PublicUrl => Existing?.PublishedAt is null || string.IsNullOrWhiteSpace(configuration["BlogSite:BaseUrl"])
        ? null
        : $"{configuration["BlogSite:BaseUrl"]!.TrimEnd('/')}/{(Existing.PostType == BlogPostType.Page ? "" : "posts/")}{Existing.Slug}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadExistingAsync(cancellationToken))
        {
            return RedirectToPage("Index");
        }

        Form = BlogPostInput.From(Existing!);
        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !await TrySaveCoverAsync(cancellationToken))
        {
            await LoadExistingAsync(cancellationToken);
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        var result = await blogPostsApiClient.UpdateAsync(Id, Form, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            AddApiErrors(result, "Could not save the post.");
            await LoadExistingAsync(cancellationToken);
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = IsPage ? "Page saved." : "Post saved.";
        return RedirectToPage(new { id = Id });
    }

    private async Task<bool> LoadExistingAsync(CancellationToken cancellationToken)
    {
        var result = await blogPostsApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Blog post not found.");
            return false;
        }

        Existing = result.Value;
        return true;
    }
}
