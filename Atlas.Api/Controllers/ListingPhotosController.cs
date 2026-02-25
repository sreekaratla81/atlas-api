using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services.Storage;
using Atlas.Api.Services.Tenancy;

namespace Atlas.Api.Controllers;

/// <summary>Upload, list, and delete photos for a listing.</summary>
[ApiController]
[Route("listings/{listingId:int}/photos")]
[Produces("application/json")]
[Authorize]
public class ListingPhotosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ITenantContextAccessor _tenantAccessor;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public ListingPhotosController(AppDbContext db, IFileStorageService storage, ITenantContextAccessor tenantAccessor)
    {
        _db = db;
        _storage = storage;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>List all photos for a listing, ordered by SortOrder.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ListingPhotoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPhotos(int listingId, CancellationToken ct)
    {
        var photos = await _db.ListingPhotos
            .Where(p => p.ListingId == listingId)
            .OrderBy(p => p.SortOrder)
            .Select(p => new ListingPhotoDto
            {
                Id = p.Id,
                ListingId = p.ListingId,
                Url = p.Url,
                OriginalFileName = p.OriginalFileName,
                SortOrder = p.SortOrder,
                Caption = p.Caption,
                IsCover = p.IsCover,
            })
            .ToListAsync(ct);

        return Ok(photos);
    }

    /// <summary>Upload one or more photos to a listing.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IEnumerable<ListingPhotoDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhotos(
        int listingId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (files is null || files.Count == 0)
            return BadRequest(new { error = "At least one file is required." });

        var listing = await _db.Listings.FindAsync(new object[] { listingId }, ct);
        if (listing is null)
            return NotFound(new { error = $"Listing {listingId} not found." });

        var tenantId = _tenantAccessor.TenantId ?? 0;
        var maxSort = await _db.ListingPhotos
            .Where(p => p.ListingId == listingId)
            .MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;

        var results = new List<ListingPhotoDto>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (file.Length > MaxFileSizeBytes)
                return BadRequest(new { error = $"File '{file.FileName}' exceeds 10 MB limit." });
            if (!AllowedContentTypes.Contains(file.ContentType))
                return BadRequest(new { error = $"File type '{file.ContentType}' is not allowed. Use JPEG, PNG, WebP, or GIF." });

            var ext = Path.GetExtension(file.FileName);
            var blobName = $"t{tenantId}/listings/{listingId}/{Guid.NewGuid()}{ext}";

            await using var stream = file.OpenReadStream();
            var upload = await _storage.UploadAsync("photos", blobName, stream, file.ContentType, ct);

            maxSort++;
            var photo = new ListingPhoto
            {
                TenantId = tenantId,
                ListingId = listingId,
                Url = upload.Url,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = upload.SizeBytes,
                SortOrder = maxSort,
                IsCover = maxSort == 1,
            };

            _db.ListingPhotos.Add(photo);
            await _db.SaveChangesAsync(ct);

            results.Add(new ListingPhotoDto
            {
                Id = photo.Id,
                ListingId = photo.ListingId,
                Url = photo.Url,
                OriginalFileName = photo.OriginalFileName,
                SortOrder = photo.SortOrder,
                Caption = photo.Caption,
                IsCover = photo.IsCover,
            });
        }

        return Created($"/listings/{listingId}/photos", results);
    }

    /// <summary>Delete a photo from a listing.</summary>
    [HttpDelete("{photoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(int listingId, int photoId, CancellationToken ct)
    {
        var photo = await _db.ListingPhotos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.ListingId == listingId, ct);

        if (photo is null)
            return NotFound(new { error = "Photo not found." });

        await _storage.DeleteAsync(photo.Url, ct);
        _db.ListingPhotos.Remove(photo);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Set a photo as the cover image for a listing.</summary>
    [HttpPatch("{photoId:int}/cover")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCover(int listingId, int photoId, CancellationToken ct)
    {
        var photo = await _db.ListingPhotos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.ListingId == listingId, ct);

        if (photo is null)
            return NotFound(new { error = "Photo not found." });

        var allPhotos = await _db.ListingPhotos
            .Where(p => p.ListingId == listingId)
            .ToListAsync(ct);

        foreach (var p in allPhotos)
            p.IsCover = p.Id == photoId;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class ListingPhotoDto
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string Url { get; set; } = null!;
    public string? OriginalFileName { get; set; }
    public int SortOrder { get; set; }
    public string? Caption { get; set; }
    public bool IsCover { get; set; }
}
