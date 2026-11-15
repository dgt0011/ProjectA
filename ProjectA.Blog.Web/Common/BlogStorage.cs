namespace ProjectA.Blog.Web.Common;

public static class BlogStorage
{
    // Same setting (and same default) as ProjectA.Web's BlogFileStorage - both apps must resolve
    // to the same directory. The default "../blog-storage" is the sibling folder both projects
    // share when run side by side from the repo in development.
    public static string ResolveRoot(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["BlogStorage:RootPath"];
        var root = string.IsNullOrWhiteSpace(configured) ? "../blog-storage" : configured;
        return Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root));
    }
}
