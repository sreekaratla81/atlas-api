using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.Events;
using Atlas.Api.Models;
using Atlas.Api.Models.Dtos.Razorpay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Api.Services
{
    public interface IRazorpayPaymentService
    {
        Task<RazorpayOrderResponse> CreateOrderAsync(CreateRazorpayOrderRequest request);
        Task<bool> VerifyAndProcessPaymentAsync(VerifyRazorpayPaymentRequest request);
    }

    public class RazorpayPaymentService : IRazorpayPaymentService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _keyId;
        private readonly string _keySecret;
        private readonly ILogger<RazorpayPaymentService> _logger;
        private readonly IEmailService _emailService;
        private readonly PricingService _pricingService;
        private readonly IQuoteService _quoteService;

        public RazorpayPaymentService(
            AppDbContext context,
            IOptions<RazorpayConfig> config,
            IHttpClientFactory httpClientFactory,
            ILogger<RazorpayPaymentService> logger,
            IEmailService emailService,
            PricingService pricingService,
            IQuoteService quoteService)
        {
            _context = context;
            _keyId = config.Value.KeyId ?? throw new ArgumentNullException(nameof(config.Value.KeyId));
            _keySecret = config.Value.KeySecret ?? throw new ArgumentNullException(nameof(config.Value.KeySecret));
            _httpClient = httpClientFactory.CreateClient("Razorpay");
            _logger = logger;
            _emailService = emailService;
            _pricingService = pricingService;
            _quoteService = quoteService;

            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            _httpClient.BaseAddress = new Uri("https://api.razorpay.com/v1/");
        }

        public async Task<RazorpayOrderResponse> CreateOrderAsync(CreateRazorpayOrderRequest request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await GetOrCreateBookingAsync(request);

                var breakdown = await ResolveBreakdownForOrderAsync(request, booking);
                booking.PaymentStatus = "pending";
                booking.TotalAmount = breakdown.FinalAmount;
                booking.BaseAmount = breakdown.BaseAmount;
                booking.DiscountAmount = breakdown.DiscountAmount;
                booking.ConvenienceFeeAmount = breakdown.ConvenienceFeeAmount;
                booking.FinalAmount = breakdown.FinalAmount;
                booking.PricingSource = breakdown.PricingSource;
                booking.QuoteTokenNonce = breakdown.QuoteTokenNonce;
                booking.QuoteExpiresAtUtc = breakdown.QuoteExpiresAtUtc;

                var orderRequest = new
                {
                    amount = (int)(breakdown.FinalAmount * 100),
                    currency = request.Currency,
                    receipt = $"booking_{booking.Id}",
                    payment_capture = 1
                };

                var content = new StringContent(JsonSerializer.Serialize(orderRequest), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("orders", content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Razorpay API error: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var orderResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var orderId = orderResponse.GetProperty("id").GetString();
                if (string.IsNullOrEmpty(orderId))
                {
                    throw new InvalidOperationException("Failed to create Razorpay order: Invalid response from Razorpay");
                }

                var payment = new Payment
                {
                    BookingId = booking.Id,
                    Amount = breakdown.FinalAmount,
                    BaseAmount = breakdown.BaseAmount,
                    DiscountAmount = breakdown.DiscountAmount,
                    ConvenienceFeeAmount = breakdown.ConvenienceFeeAmount,
                    Method = "Razorpay",
                    Type = "payment",
                    ReceivedOn = DateTime.UtcNow,
                    Note = $"Razorpay Order ID: {orderId}",
                    RazorpayOrderId = orderId,
                    Status = "pending"
                };

                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(payment, new ValidationContext(payment), validationResults, true))
                {
                    throw new ValidationException($"Invalid payment data: {string.Join(", ", validationResults.Select(v => v.ErrorMessage))}");
                }

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new RazorpayOrderResponse
                {
                    KeyId = _keyId,
                    OrderId = orderId,
                    Amount = breakdown.FinalAmount,
                    Currency = request.Currency,
                    BookingId = booking.Id
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> VerifyAndProcessPaymentAsync(VerifyRazorpayPaymentRequest request)
        {
            var booking = await _context.Bookings.FindAsync(request.BookingId)
                ?? throw new ArgumentException($"Invalid booking ID: {request.BookingId}");

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == request.BookingId && p.RazorpayOrderId == request.RazorpayOrderId)
                ?? throw new InvalidOperationException("Payment record not found for the given booking and order ID");

            // FD-001 idempotency guard: if payment already completed, return success without side effects
            if (string.Equals(payment.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Idempotent verify: payment already completed for BookingId={BookingId}, RazorpayOrderId={OrderId}",
                    request.BookingId, request.RazorpayOrderId);
                return true;
            }

            var text = $"{request.RazorpayOrderId}|{request.RazorpayPaymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
            var computedSignature = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLower();

            if (computedSignature != request.RazorpaySignature.ToLower())
            {
                booking.PaymentStatus = "failed";
                payment.Status = "failed";
                await _context.SaveChangesAsync();
                return false;
            }

            payment.RazorpayPaymentId = request.RazorpayPaymentId;
            payment.RazorpaySignature = request.RazorpaySignature;
            payment.Status = "completed";
            payment.ReceivedOn = DateTime.UtcNow;
            payment.Note = $"Razorpay Payment ID: {request.RazorpayPaymentId}";

            booking.PaymentStatus = "paid";
            booking.AmountReceived = booking.TotalAmount ?? 0;
            if (string.Equals(booking.BookingStatus, BookingStatuses.Hold, StringComparison.OrdinalIgnoreCase))
            {
                booking.BookingStatus = BookingStatuses.Confirmed;
            }

            var temporaryBlocks = await _context.AvailabilityBlocks
                .Where(ab => ab.BookingId == booking.Id && ab.BlockType == BlockStatuses.Hold && ab.Status == BlockStatuses.Hold)
                .ToListAsync();
            foreach (var block in temporaryBlocks)
            {
                block.BlockType = "Booking";
                block.Source = "System";
                block.Status = BlockStatuses.Active;
                block.Inventory = false;
                block.UpdatedAtUtc = DateTime.UtcNow;
            }

            // FD-001 outbox parity: emit booking.confirmed so notification consumers fire (same as manual flow)
            var guest = await _context.Guests.FindAsync(booking.GuestId);
            if (guest != null)
            {
                var outboxPayload = JsonSerializer.Serialize(new
                {
                    bookingId = booking.Id,
                    guestId = guest.Id,
                    listingId = booking.ListingId,
                    bookingStatus = booking.BookingStatus,
                    checkinDate = booking.CheckinDate,
                    checkoutDate = booking.CheckoutDate,
                    guestPhone = guest.Phone,
                    guestEmail = guest.Email,
                    occurredAtUtc = DateTime.UtcNow
                });
                _context.OutboxMessages.Add(new OutboxMessage
                {
                    Topic = "booking.events",
                    EventType = EventTypes.BookingConfirmed,
                    EntityId = booking.Id.ToString(),
                    PayloadJson = outboxPayload,
                    CorrelationId = Guid.NewGuid().ToString(),
                    OccurredUtc = DateTime.UtcNow,
                    SchemaVersion = 1,
                    Status = "Pending",
                    NextAttemptUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    AttemptCount = 0
                });
            }

            await _context.SaveChangesAsync();

            try
            {
                var emailSent = await _emailService.SendBookingConfirmationEmailAsync(booking, request.RazorpayPaymentId);
                if (emailSent)
                {
                    booking.ConfirmationSentAtUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Error sending booking confirmation email for booking ID: {BookingId}. Payment verification succeeded.", request.BookingId);
            }

            return true;
        }

        private async Task<Atlas.Api.DTOs.PriceBreakdownDto> ResolveBreakdownForOrderAsync(CreateRazorpayOrderRequest request, Booking booking)
        {
            if (!string.IsNullOrWhiteSpace(request.QuoteToken))
            {
                var quoteValidation = await _quoteService.ValidateForRedemptionAsync(request.QuoteToken, booking.Id);
                if (!quoteValidation.IsValid || quoteValidation.Breakdown is null)
                {
                    throw new InvalidOperationException(quoteValidation.Error ?? "Invalid quote token.");
                }

                return quoteValidation.Breakdown;
            }

            // Use client-provided total (widget price breakdown total) so Razorpay checkout shows the same amount
            if (request.Amount.HasValue && request.Amount.Value > 0)
            {
                return new Atlas.Api.DTOs.PriceBreakdownDto
                {
                    ListingId = booking.ListingId,
                    Currency = request.Currency,
                    BaseAmount = request.Amount.Value,
                    DiscountAmount = 0,
                    ConvenienceFeeAmount = 0,
                    FinalAmount = request.Amount.Value,
                    PricingSource = "Client"
                };
            }

            if (request.BookingDraft is not null)
            {
                return await _pricingService.GetPublicBreakdownAsync(request.BookingDraft.ListingId, request.BookingDraft.CheckinDate, request.BookingDraft.CheckoutDate);
            }

            if (booking.TotalAmount.HasValue)
            {
                return new Atlas.Api.DTOs.PriceBreakdownDto
                {
                    ListingId = booking.ListingId,
                    Currency = booking.Currency,
                    BaseAmount = booking.BaseAmount ?? booking.TotalAmount.Value,
                    DiscountAmount = booking.DiscountAmount ?? 0,
                    ConvenienceFeeAmount = booking.ConvenienceFeeAmount ?? 0,
                    FinalAmount = booking.FinalAmount ?? booking.TotalAmount.Value,
                    PricingSource = booking.PricingSource
                };
            }

            if (request.Amount.HasValue)
            {
                return new Atlas.Api.DTOs.PriceBreakdownDto
                {
                    ListingId = booking.ListingId,
                    Currency = request.Currency,
                    BaseAmount = request.Amount.Value,
                    DiscountAmount = 0,
                    ConvenienceFeeAmount = 0,
                    FinalAmount = request.Amount.Value,
                    PricingSource = "Manual"
                };
            }

            throw new InvalidOperationException("Unable to determine pricing for Razorpay order.");
        }

        private async Task<Booking> GetOrCreateBookingAsync(CreateRazorpayOrderRequest request)
        {
            if (request.BookingId.HasValue)
            {
                var existing = await _context.Bookings.FindAsync(request.BookingId.Value);
                if (existing is not null)
                {
                    return existing;
                }
            }

            if (request.BookingDraft is null)
            {
                throw new ArgumentException("Either bookingId or bookingDraft must be provided");
            }

            var listingExists = await _context.Listings.AnyAsync(l => l.Id == request.BookingDraft.ListingId);
            if (!listingExists)
            {
                throw new ArgumentException($"Listing with ID {request.BookingDraft.ListingId} does not exist");
            }

            var guest = await _context.Guests
                .FirstOrDefaultAsync(g => g.Email == request.GuestInfo.Email) ?? new Guest
                {
                    Name = string.IsNullOrWhiteSpace(request.GuestInfo.Name) ? "Guest User" : request.GuestInfo.Name,
                    Email = request.GuestInfo.Email,
                    Phone = request.GuestInfo.Phone
                };

            if (guest.Id == 0)
            {
                _context.Guests.Add(guest);
                await _context.SaveChangesAsync();
            }

            var booking = new Booking
            {
                ListingId = request.BookingDraft.ListingId,
                GuestId = guest.Id,
                CheckinDate = request.BookingDraft.CheckinDate,
                CheckoutDate = request.BookingDraft.CheckoutDate,
                GuestsPlanned = request.BookingDraft.Guests,
                Notes = request.BookingDraft.Notes ?? string.Empty,
                BookingStatus = BookingStatuses.Hold,
                PaymentStatus = "pending",
                Currency = request.Currency,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var blocks = new List<AvailabilityBlock>();
            for (var d = booking.CheckinDate.Date; d < booking.CheckoutDate.Date; d = d.AddDays(1))
            {
                blocks.Add(new AvailabilityBlock
                {
                    ListingId = booking.ListingId,
                    BookingId = booking.Id,
                    StartDate = d,
                    EndDate = d.AddDays(1),
                    BlockType = BlockStatuses.Hold,
                    Source = "Razorpay",
                    Status = BlockStatuses.Hold,
                    Inventory = false,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            _context.AvailabilityBlocks.AddRange(blocks);
            await _context.SaveChangesAsync();

            return booking;
        }
    }
}
