namespace Atlas.Api.Services.Storage;

public record FileUploadResult(string Url, long SizeBytes, string ContentType);

public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(
        string containerOrFolder,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken ct = default);

    Task DeleteAsync(string url, CancellationToken ct = default);
}
