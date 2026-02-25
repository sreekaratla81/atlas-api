using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Atlas.Api.Services.Storage;

public sealed class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobServiceClient _client;

    public AzureBlobStorageService(BlobServiceClient client)
    {
        _client = client;
    }

    public async Task<FileUploadResult> UploadAsync(
        string container, string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        var containerClient = _client.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct);

        return new FileUploadResult(blobClient.Uri.ToString(), content.Length, contentType);
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        if (segments.Length < 2) return;

        var containerClient = _client.GetBlobContainerClient(segments[0]);
        await containerClient.DeleteBlobIfExistsAsync(segments[1], cancellationToken: ct);
    }
}
