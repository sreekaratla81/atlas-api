namespace Atlas.Api.Services.Storage;

/// <summary>
/// Stores files on local disk under wwwroot/uploads. Used when Azure Blob is not configured.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalFileStorageService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
    {
        _env = env;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<FileUploadResult> UploadAsync(
        string containerOrFolder, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        var root = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", containerOrFolder);
        Directory.CreateDirectory(root);

        var filePath = Path.Combine(root, blobName);
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fs, ct);

        var request = _httpContextAccessor.HttpContext?.Request;
        var baseUrl = request is not null ? $"{request.Scheme}://{request.Host}" : "";
        var url = $"{baseUrl}/uploads/{containerOrFolder}/{blobName}";
        return new FileUploadResult(url, fs.Length, contentType);
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var relativePath = uri.AbsolutePath.TrimStart('/');
            var fullPath = Path.Combine(_env.ContentRootPath, "wwwroot", relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }
}
