using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog;

// Blog dashboard - Blogifier's admin home (totals + views of the latest posts), plus views per
// day for the whole blog or one post/page. Clicking a day loads that day's visitors (IP address
// + user agent) into a modal via the Visitors handler.
[Authorize]
public class IndexModel(IBlogAdminApiClient blogAdminApiClient, IConfiguration configuration) : PageModel
{
    public static readonly int[] LatestOptions = [7, 30, 90];
    public static readonly int[] DaysOptions = [7, 30, 90, 365];

    [BindProperty(SupportsGet = true)]
    public int Latest { get; set; } = 7;

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 30;

    // Null = the whole blog.
    [BindProperty(SupportsGet = true)]
    public long? PostId { get; set; }

    public BlogAnalyticsDto? Analytics { get; private set; }

    public BlogDailyViewsDto? DailyViews { get; private set; }

    public string? BlogSiteUrl => configuration["BlogSite:BaseUrl"];

    public string DailyViewsLabel =>
        PostId is null
            ? "All posts and pages"
            : DailyViews?.Posts.FirstOrDefault(p => p.Id == PostId)?.Title ?? "Unknown post";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!LatestOptions.Contains(Latest))
        {
            Latest = LatestOptions[0];
        }

        if (!DaysOptions.Contains(Days))
        {
            Days = 30;
        }

        var analyticsTask = blogAdminApiClient.GetAnalyticsAsync(Latest, cancellationToken);
        var dailyTask = blogAdminApiClient.GetDailyViewsAsync(Days, PostId, cancellationToken);

        var result = await analyticsTask;
        if (result.IsSuccess)
        {
            Analytics = result.Value;
        }
        else
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Could not load blog analytics.");
        }

        var daily = await dailyTask;
        if (daily.IsSuccess)
        {
            DailyViews = daily.Value;
        }
        else if (result.IsSuccess)
        {
            TempData["ErrorMessage"] = daily.ToDisplayMessage("Could not load daily blog views.");
        }
    }

    // GET /Blog?handler=Visitors&day=yyyy-MM-dd[&postId=n] - the modal body for one day.
    public async Task<IActionResult> OnGetVisitorsAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var result = await blogAdminApiClient.GetDailyVisitorsAsync(day, PostId, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return Content(
                $"<p class=\"text-danger mb-0\">{System.Net.WebUtility.HtmlEncode(result.ToDisplayMessage("Could not load visitors."))}</p>",
                "text/html");
        }

        return Partial("_BlogVisitors", result.Value);
    }
}
