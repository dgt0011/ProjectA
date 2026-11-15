using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog.Posts;

// Drafts never appear on the public blog, so this is where an author reads one rendered
// before publishing (Blogifier let signed-in authors open drafts on the public site instead).
[Authorize]
public class PreviewModel(IBlogPostsApiClient blogPostsApiClient) : PageModel
{
    public BlogPostDto Post { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var result = await blogPostsApiClient.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Blog post not found.");
            return RedirectToPage("Index");
        }

        Post = result.Value;
        return Page();
    }
}
