namespace ProjectA.Web.Common;

// Saves blog uploads (cover images, images inserted into posts, avatars) into the shared blog
// storage folder that ProjectA.Blog.Web serves at /data - in production both apps point
// BlogStorage:RootPath at the same EFS directory. Returned paths are site-relative
// ("/data/2026/11/....png") so they work unchanged on the public blog, and also in this app's
// own previews, since Program.cs serves the same folder at /data here too.
//
// Migrated Blogifier files live under data/{authorId}/... (e.g. data/2/626.jpg); new uploads go
// under data/{yyyy}/{MM}/, which can never collide with those single-digit author folders.
public sealed class BlogFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
{
    public const string RequestPath = "/data";

    // Deliberately excludes .svg/.html/.js - files here are served from the public blog's own
    // origin, so anything a browser would execute is refused outright.
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".gif", ".jpg", ".jpeg", ".webp",
        ".pdf", ".zip", ".7z", ".doc", ".docx", ".xls", ".xlsx", ".mp3", ".mp4"
    };

    public static readonly long MaxFileBytes = 25 * 1024 * 1024;

    // Linux/EFS allow 255 bytes per file name; the stored name adds a 9-character prefix, and
    // non-Latin names take up to 4 UTF-8 bytes a character, so the client's name is cut to this.
    public const int MaxNameBytes = 150;

    // Sent with every file served from /data (ProjectA.Blog.Web uses the same policy): images
    // and media display, but a file opened as a document can't run script or load anything.
    public const string UploadContentSecurityPolicy = "default-src 'none'; img-src 'self'; media-src 'self'; style-src 'unsafe-inline'; sandbox";

    // Shortens the name part (keeping the extension, which IsAllowed has already checked) until
    // the whole name fits MaxNameBytes, without splitting a surrogate pair.
    internal static string LimitLength(string fileName)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(fileName) <= MaxNameBytes)
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        while (stem.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(stem + extension) > MaxNameBytes)
        {
            stem = stem[..^1];
            if (stem.Length > 0 && char.IsHighSurrogate(stem[^1]))
            {
                stem = stem[..^1];
            }
        }

        return (stem.Length > 0 ? stem : "file") + extension;
    }

    public string RootPath
    {
        get
        {
            var configured = configuration["BlogStorage:RootPath"];
            var root = string.IsNullOrWhiteSpace(configured) ? "../blog-storage" : configured;
            return Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root));
        }
    }

    public static bool IsAllowed(IFormFile file, out string error)
    {
        error = string.Empty;
        if (file.Length == 0)
        {
            error = "The uploaded file is empty.";
            return false;
        }

        if (file.Length > MaxFileBytes)
        {
            error = "Files must be 25 MB or smaller.";
            return false;
        }

        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName)))
        {
            error = $"Only these file types can be uploaded: {string.Join(", ", AllowedExtensions.Order())}.";
            return false;
        }

        return true;
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var relativeFolder = Path.Combine(now.ToString("yyyy"), now.ToString("MM"));
        var folder = Path.Combine(RootPath, relativeFolder);
        Directory.CreateDirectory(folder);

        // Path.GetFileName strips any directory part a client smuggles into the name; the short
        // random prefix stops two uploads with the same name overwriting each other while
        // keeping the original name readable in the URL.
        var originalName = LimitLength(Path.GetFileName(file.FileName).Replace(' ', '-'));
        var storedName = $"{Guid.NewGuid().ToString("N")[..8]}-{originalName}";

        await using (var stream = new FileStream(Path.Combine(folder, storedName), FileMode.CreateNew))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"{RequestPath}/{now:yyyy}/{now:MM}/{Uri.EscapeDataString(storedName)}";
    }
}
