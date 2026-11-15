using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog.Posts;

// Create and Edit render the same _PostForm partial, so the bound form, the optional cover
// upload and the category suggestions live here once.
public abstract class BlogPostFormPageModel(
    IBlogCategoriesApiClient blogCategoriesApiClient,
    BlogFileStorage blogFileStorage) : PageModel
{
    [BindProperty]
    public BlogPostInput Form { get; set; } = new();

    [BindProperty]
    public IFormFile? CoverFile { get; set; }

    public List<string> AvailableCategories { get; private set; } = [];

    public bool IsPage => Form.PostType == BlogPostType.Page;

    protected async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var categories = await blogCategoriesApiClient.GetListAsync(cancellationToken);
        AvailableCategories = categories.IsSuccess
            ? (categories.Value ?? []).Select(c => c.Title).ToList()
            : [];
    }

    // Saves a newly chosen cover (if any) into blog storage and points Form.Cover at it.
    // Returns false (with a ModelState error) when the file is rejected.
    protected async Task<bool> TrySaveCoverAsync(CancellationToken cancellationToken)
    {
        if (CoverFile is null)
        {
            return true;
        }

        if (!BlogFileStorage.IsAllowed(CoverFile, out var error))
        {
            ModelState.AddModelError(nameof(CoverFile), error);
            return false;
        }

        Form.Cover = await blogFileStorage.SaveAsync(CoverFile, cancellationToken);

        // If the API then rejects the save, the form is re-rendered: without this the Cover box
        // would show the originally posted value (ModelState wins over the model) and the new
        // upload would be silently dropped. With it, a resubmit reuses the file just stored.
        ModelState.Remove($"{nameof(Form)}.{nameof(Form.Cover)}");
        return true;
    }

    protected void AddApiErrors(ApiResult<BlogPostDto> result, string fallback)
    {
        ModelState.AddApiErrors(result.Errors);
        if (result.Errors is null)
        {
            ModelState.AddModelError(string.Empty, result.ToDisplayMessage(fallback));
        }
    }
}
