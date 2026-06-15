namespace WorkshopZagreb.Services;

public interface IFileService
{
    Task<string> SaveImageAsync(IFormFile file, string subfolder);
    void DeleteImage(string relativePath);
}

// On Azure you'd swap the body of SaveImageAsync to upload to Azure Blob Storage instead.
// The interface stays the same — the rest of the app doesn't need to change at all.
public class FileService : IFileService
{
    private readonly IWebHostEnvironment _env;

    // On Azure, wwwroot is read-only and replaced on every deploy ("Run From
    // Package"), so uploads must go to /home/data/images (persistent storage)
    // instead. Locally this is just wwwroot/images as before.
    private readonly string _imagesRoot;

    public FileService(IWebHostEnvironment env)
    {
        _env = env;
        _imagesRoot = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"))
            ? "/home/data/images"
            : Path.Combine(_env.WebRootPath, "images");
    }

    public async Task<string> SaveImageAsync(IFormFile file, string subfolder)
    {
        var uploadsFolder = Path.Combine(_imagesRoot, subfolder);
        Directory.CreateDirectory(uploadsFolder);

        // Create a unique filename to prevent collisions
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsFolder, fileName);

        using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        // Return the relative URL — this is what gets stored in the database
        // and used directly in <img src="...">
        return $"/images/{subfolder}/{fileName}";
    }

    public void DeleteImage(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        // relativePath looks like "/images/{subfolder}/{file}" — strip the leading "/images/"
        var trimmed = relativePath.TrimStart('/');
        if (trimmed.StartsWith("images/")) trimmed = trimmed["images/".Length..];

        var fullPath = Path.Combine(_imagesRoot, trimmed);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
