namespace ProjectA.Web.Common;

// Saves an uploaded Attachment file locally under wwwroot/files and returns the web-relative
// path (e.g. "/files/3f2c1a9e-report.pdf") that gets stored as the Attachment's FilePath.
// UseStaticFiles() serves wwwroot as-is, so this path works immediately as a direct
// download/view link - no extra routing or a controller action needed to serve it back.
public static class AttachmentFileStorage
{
    private const string RelativeFolder = "files";

    public static async Task<string> SaveAsync(
        IWebHostEnvironment environment, IFormFile file, CancellationToken cancellationToken)
    {
        var folderPath = Path.Combine(environment.WebRootPath, RelativeFolder);
        Directory.CreateDirectory(folderPath);

        // Guid-prefixed so two uploads that happen to share a filename never collide/overwrite
        // each other - the original filename is kept after the prefix purely so the stored path
        // stays human-readable (e.g. in the Index page or API responses). Path.GetFileName
        // strips any directory portion a hostile/misbehaving client might smuggle into
        // file.FileName, so the saved file always lands inside RelativeFolder, never above it.
        var originalFileName = Path.GetFileName(file.FileName);
        var storedFileName = $"{Guid.NewGuid():N}-{originalFileName}";
        var fullPath = Path.Combine(folderPath, storedFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/{RelativeFolder}/{storedFileName}";
    }
}
