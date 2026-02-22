using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services.Onboarding;

public class OnboardingService
{
    private readonly AppDbContext _db;

    public OnboardingService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Seeds the default onboarding checklist for a newly created tenant.</summary>
    public async Task SeedChecklistAsync(int tenantId, CancellationToken ct)
    {
        var items = new[]
        {
            new OnboardingChecklistItem { TenantId = tenantId, Key = "basic_info", Title = "Basic property information", Stage = "FastStart", Status = "Complete", Blocking = true },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "contact_info", Title = "Contact email & phone", Stage = "FastStart", Status = "Complete", Blocking = true },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "pan_upload", Title = "Upload PAN card", Stage = "PublishGate", Status = "Pending", Blocking = true },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "bank_account", Title = "Add bank account for payouts", Stage = "PublishGate", Status = "Pending", Blocking = true },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "listing_photos", Title = "Add at least 3 property photos", Stage = "PublishGate", Status = "Pending", Blocking = true },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "pricing_set", Title = "Set nightly pricing", Stage = "PublishGate", Status = "Pending", Blocking = true },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "gstin_optional", Title = "Add GSTIN (if registered)", Stage = "PostPublish", Status = "Pending", Blocking = false },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "tourism_reg", Title = "State tourism registration", Stage = "PostPublish", Status = "Pending", Blocking = false, DueAtUtc = DateTime.UtcNow.AddDays(90) },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "fire_noc", Title = "Fire safety NOC", Stage = "PostPublish", Status = "Pending", Blocking = false, DueAtUtc = DateTime.UtcNow.AddDays(90) },
            new OnboardingChecklistItem { TenantId = tenantId, Key = "owner_noc", Title = "Owner NOC (if leased property)", Stage = "PostPublish", Status = "Pending", Blocking = false },
        };

        _db.OnboardingChecklistItems.AddRange(items);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Computes publish blockers: PublishGate items that are Blocking + not Complete.</summary>
    public async Task<List<string>> GetPublishBlockersAsync(int tenantId, CancellationToken ct)
    {
        return await _db.OnboardingChecklistItems
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.Stage == "PublishGate" && i.Blocking && i.Status != "Complete")
            .Select(i => i.Title)
            .ToListAsync(ct);
    }

    /// <summary>Try to extract metadata from Airbnb listing URL (public HTML only).</summary>
    public async Task<DTOs.AirbnbPrefillResponseDto> ExtractAirbnbMetadataAsync(string? url, string? pastedText)
    {
        var result = new DTOs.AirbnbPrefillResponseDto { Source = "none" };

        if (!string.IsNullOrWhiteSpace(pastedText))
        {
            result.Source = "pasted_text";
            result.Title = ExtractBetween(pastedText, "", "\n")?.Trim();
            result.Warnings.Add("Extracted from pasted text. Please verify all fields.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(url) || !url.Contains("airbnb", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add("No valid Airbnb URL or text provided.");
            return result;
        }

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; AtlasBot/1.0)");
            http.Timeout = TimeSpan.FromSeconds(10);

            var html = await http.GetStringAsync(url);
            result.Source = "airbnb_url";

            result.Title = ExtractMetaContent(html, "og:title");
            result.Description = ExtractMetaContent(html, "og:description");
            result.LocationText = ExtractMetaContent(html, "og:locality")
                ?? ExtractMetaContent(html, "og:region");

            if (string.IsNullOrWhiteSpace(result.Title))
                result.Warnings.Add("Could not extract title from Airbnb page.");
            else
                result.Warnings.Add("Extracted from public Airbnb metadata. Please verify all fields before saving.");
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Failed to fetch Airbnb URL: {ex.Message}");
        }

        return result;
    }

    private static string? ExtractMetaContent(string html, string property)
    {
        var marker = $"property=\"{property}\" content=\"";
        var idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            marker = $"name=\"{property}\" content=\"";
            idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        }
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var end = html.IndexOf('"', start);
        return end > start ? System.Net.WebUtility.HtmlDecode(html[start..end]) : null;
    }

    private static string? ExtractBetween(string text, string startMarker, string endMarker)
    {
        var start = string.IsNullOrEmpty(startMarker) ? 0 : text.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += startMarker.Length;
        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        return end > start ? text[start..end] : text[start..];
    }
}
