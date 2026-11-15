namespace ProjectA.Blog.Web.Common;

public static class BlogStorage
{
    // Sent with every file served from /data (also by ProjectA.Web, which serves the same folder):
    // images and media still display, but a file opened as a document can't run script, load
    // anything, or be treated as same-origin.
    public const string UploadContentSecurityPolicy = "default-src 'none'; img-src 'self'; media-src 'self'; style-src 'unsafe-inline'; sandbox";

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
