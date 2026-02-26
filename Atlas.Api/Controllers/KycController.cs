using Atlas.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers;

[ApiController]
[Authorize(Roles = "platform-admin")]
[Route("kyc")]
[Produces("application/json")]
public class KycController : ControllerBase
{
    private readonly AppDbContext _db;

    public KycController(AppDbContext db) => _db = db;

    [HttpGet("documents")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDocuments(CancellationToken ct)
    {
        var docs = await _db.HostKycDocuments
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new
            {
                d.Id,
                d.TenantId,
                d.DocType,
                d.FileUrl,
                d.OriginalFileName,
                d.Status,
                d.Notes,
                d.VerifiedAtUtc,
                d.CreatedAtUtc
            })
            .ToListAsync(ct);
        return Ok(docs);
    }

    [HttpPut("documents/{id:int}/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyDocument(int id, [FromBody] VerifyDocDto dto, CancellationToken ct)
    {
        var doc = await _db.HostKycDocuments.FindAsync(new object[] { id }, ct);
        if (doc is null) return NotFound(new { error = "Document not found." });

        doc.Status = dto.Approved ? "Approved" : "Rejected";
        doc.Notes = dto.Notes;
        doc.VerifiedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new { doc.Id, doc.Status, doc.Notes, doc.VerifiedAtUtc });
    }

    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var docs = await _db.HostKycDocuments.AsNoTracking().ToListAsync(ct);
        var requiredTypes = new[] { "PAN", "Aadhaar" };
        var hasAllRequired = requiredTypes.All(rt =>
            docs.Any(d => d.DocType == rt && d.Status == "Approved"));
        var pendingCount = docs.Count(d => d.Status == "Pending");
        var rejectedCount = docs.Count(d => d.Status == "Rejected");

        var overall = hasAllRequired
            ? "Verified"
            : pendingCount > 0
                ? "Pending"
                : docs.Count == 0
                    ? "Not Started"
                    : "Incomplete";

        return Ok(new
        {
            status = overall,
            documentsCount = docs.Count,
            pendingCount,
            rejectedCount,
            hasAllRequired
        });
    }
}

public class VerifyDocDto
{
    public bool Approved { get; set; }
    public string? Notes { get; set; }
}
