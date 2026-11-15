namespace ProjectA.Web.Security;

// The blog admin area and its own (blog-styled) login page. Requests under Area that need
// sign-in are sent to Path rather than ProjectA's /Account/Login (see Program.cs), and logging
// out from the area returns there too.
public static class BlogLoginPath
{
    public static readonly PathString Area = "/Blog";
    public static readonly PathString Path = "/Blog/Login";

    public static bool IsBlogArea(HttpRequest request) => request.Path.StartsWithSegments(Area);
}
