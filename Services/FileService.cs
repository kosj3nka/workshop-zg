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

    public FileService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveImageAsync(IFormFile file, string subfolder)
    {
        // Build the save path inside /wwwroot/images/{subfolder}/
        var uploadsFolder = Path.Combine(_env.WebRootPath, "images", subfolder);
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
        var fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
