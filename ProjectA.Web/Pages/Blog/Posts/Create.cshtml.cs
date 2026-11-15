using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog.Posts;

[Authorize]
public class CreateModel(
    IBlogPostsApiClient blogPostsApiClient,
    IBlogCategoriesApiClient blogCategoriesApiClient,
    BlogFileStorage blogFileStorage) : BlogPostFormPageModel(blogCategoriesApiClient, blogFileStorage)
{
    public async Task OnGetAsync(short? type, CancellationToken cancellationToken)
    {
        Form = new BlogPostInput { PostType = type == BlogPostType.Page ? BlogPostType.Page : BlogPostType.Post };
        await LoadLookupsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !await TrySaveCoverAsync(cancellationToken))
        {
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        var result = await blogPostsApiClient.CreateAsync(Form, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            AddApiErrors(result, "Could not create the post.");
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        TempData["SuccessMessage"] = IsPage ? "Page created." : "Post created.";
        return RedirectToPage("Edit", new { id = result.Value.Id });
    }
}
