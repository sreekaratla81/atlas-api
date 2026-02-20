using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Atlas.Api.Services;

public class QuoteOptions
{
    public string SigningKey { get; set; } = string.Empty;
}

public interface IQuoteService
{
    Task<QuoteIssueResponseDto> IssueAsync(CreateQuoteRequestDto request, CancellationToken cancellationToken = default);
    Task<QuoteValidateResponseDto> ValidateAsync(string token, CancellationToken cancellationToken = default);
    Task<QuoteValidateResponseDto> ValidateForRedemptionAsync(string token, int? bookingId, CancellationToken cancellationToken = default);
}

internal sealed class QuotePayload
{
    public int TenantId { get; set; }
    public int ListingId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Guests { get; set; }
    public decimal QuotedBaseAmount { get; set; }
    public string FeeMode { get; set; } = "CustomerPays";
    public DateTime ExpUtc { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public bool ApplyGlobalDiscount { get; set; }
}

public class QuoteService : IQuoteService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ITenantPricingSettingsService _tenantPricingSettingsService;
    private readonly PricingService _pricingService;
    private readonly AppDbContext _dbContext;
    private readonly byte[] _signingKey;

    public QuoteService(
        ITenantContextAccessor tenantContextAccessor,
        ITenantPricingSettingsService tenantPricingSettingsService,
        PricingService pricingService,
        AppDbContext dbContext,
        IOptions<QuoteOptions> options)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _tenantPricingSettingsService = tenantPricingSettingsService;
        _pricingService = pricingService;
        _dbContext = dbContext;
        _signingKey = Encoding.UTF8.GetBytes(options.Value.SigningKey ?? string.Empty);
    }

    public async Task<QuoteIssueResponseDto> IssueAsync(CreateQuoteRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContextAccessor.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var payload = new QuotePayload
        {
            TenantId = tenantId,
            ListingId = request.ListingId,
            CheckIn = request.CheckIn.Date,
            CheckOut = request.CheckOut.Date,
            Guests = request.Guests,
            QuotedBaseAmount = request.QuotedBaseAmount,
            FeeMode = request.FeeMode,
            ExpUtc = request.ExpiresAtUtc,
            Nonce = Guid.NewGuid().ToString("N")[..20],
            ApplyGlobalDiscount = request.ApplyGlobalDiscount
        };

        var breakdown = await BuildBreakdownAsync(payload, cancellationToken);
        var token = SignPayload(payload);
        return new QuoteIssueResponseDto { Token = token, Breakdown = breakdown };
    }

    public Task<QuoteValidateResponseDto> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        return ValidateInternalAsync(token, false, null, cancellationToken);
    }

    public Task<QuoteValidateResponseDto> ValidateForRedemptionAsync(string token, int? bookingId, CancellationToken cancellationToken = default)
    {
        return ValidateInternalAsync(token, true, bookingId, cancellationToken);
    }

    private async Task<QuoteValidateResponseDto> ValidateInternalAsync(string token, bool redeem, int? bookingId, CancellationToken cancellationToken)
    {
        try
        {
            var payload = ParseAndVerify(token);
            var tenantId = _tenantContextAccessor.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
            if (payload.TenantId != tenantId)
            {
                return new QuoteValidateResponseDto { IsValid = false, Error = "Quote token tenant mismatch." };
            }

            if (payload.ExpUtc < DateTime.UtcNow)
            {
                return new QuoteValidateResponseDto { IsValid = false, Error = "Quote token expired." };
            }

            if (redeem)
            {
                var redemption = new QuoteRedemption
                {
                    Nonce = payload.Nonce,
                    RedeemedAtUtc = DateTime.UtcNow,
                    BookingId = bookingId
                };
                _dbContext.QuoteRedemptions.Add(redemption);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var breakdown = await BuildBreakdownAsync(payload, cancellationToken);
            return new QuoteValidateResponseDto { IsValid = true, Breakdown = breakdown };
        }
        catch (DbUpdateException)
        {
            return new QuoteValidateResponseDto { IsValid = false, Error = "Quote nonce already redeemed for this tenant." };
        }
        catch (Exception ex)
        {
            return new QuoteValidateResponseDto { IsValid = false, Error = ex.Message };
        }
    }

    private async Task<PriceBreakdownDto> BuildBreakdownAsync(QuotePayload payload, CancellationToken cancellationToken)
    {
        var quote = await _pricingService.GetPricingAsync(payload.ListingId, payload.CheckIn, payload.CheckOut);
        var settings = await _tenantPricingSettingsService.GetCurrentAsync(cancellationToken);
        return PricingService.BuildBreakdown(
            payload.ListingId,
            quote.Currency,
            payload.QuotedBaseAmount,
            settings,
            "Quoted",
            payload.FeeMode,
            payload.ApplyGlobalDiscount,
            payload.Nonce,
            payload.ExpUtc);
    }

    private string SignPayload(QuotePayload payload)
    {
        if (_signingKey.Length == 0)
        {
            throw new InvalidOperationException("QuoteSigningKey is not configured.");
        }

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        using var hmac = new HMACSHA256(_signingKey);
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64)));
        return $"{payloadB64}.{signature}";
    }

    private QuotePayload ParseAndVerify(string token)
    {
        if (_signingKey.Length == 0)
        {
            throw new InvalidOperationException("QuoteSigningKey is not configured.");
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Invalid quote token format.");
        }

        using var hmac = new HMACSHA256(_signingKey);
        var expected = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(parts[0])));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[1])))
        {
            throw new InvalidOperationException("Quote token signature is invalid.");
        }

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        return JsonSerializer.Deserialize<QuotePayload>(payloadJson) ?? throw new InvalidOperationException("Quote payload is invalid.");
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        if (padded.Length % 4 != 0)
        {
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4), '=');
        }

        return Convert.FromBase64String(padded);
    }
}
