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
    public class RazorpayRefundResult
    {
        public bool Success { get; set; }
        public string? RefundId { get; set; }
        public string? Status { get; set; }
        public decimal AmountRefunded { get; set; }
        public string? Error { get; set; }
    }

    public interface IRazorpayPaymentService
    {
        Task<RazorpayOrderResponse> CreateOrderAsync(CreateRazorpayOrderRequest request);
        Task<bool> VerifyAndProcessPaymentAsync(VerifyRazorpayPaymentRequest request);
        Task<bool> ReconcileWebhookPaymentAsync(string razorpayOrderId, string razorpayPaymentId);
        Task<RazorpayRefundResult> RefundPaymentAsync(int bookingId, decimal amount, string reason);
    }

    public class RazorpayPaymentService : IRazorpayPaymentService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _keyId;
        private readonly string _keySecret;
        private readonly ILogger<RazorpayPaymentService> _logger;
        private readonly PricingService _pricingService;
        private readonly IQuoteService _quoteService;

        public RazorpayPaymentService(
            AppDbContext context,
            IOptions<RazorpayConfig> config,
            IHttpClientFactory httpClientFactory,
            ILogger<RazorpayPaymentService> logger,
            PricingService pricingService,
            IQuoteService quoteService)
        {
            _context = context;
            _keyId = config.Value.KeyId ?? throw new ArgumentNullException(nameof(config.Value.KeyId));
            _keySecret = config.Value.KeySecret ?? throw new ArgumentNullException(nameof(config.Value.KeySecret));
            _httpClient = httpClientFactory.CreateClient("Razorpay");
            _logger = logger;
            _pricingService = pricingService;
            _quoteService = quoteService;

            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            _httpClient.BaseAddress = new Uri("https://api.razorpay.com/v1/");
        }

        public async Task<RazorpayOrderResponse> CreateOrderAsync(CreateRazorpayOrderRequest request)
        {
            // Reject duplicate: if user pressed back after successful payment and resubmitted,
            // do not create a new booking for the same listing + dates + guest.
            if (request.BookingDraft != null)
            {
                var guestEmails = await _context.Bookings
                    .AsNoTracking()
                    .Where(b => b.ListingId == request.BookingDraft.ListingId
                             && b.CheckinDate == request.BookingDraft.CheckinDate
                             && b.CheckoutDate == request.BookingDraft.CheckoutDate
                             && (b.BookingStatus == BookingStatuses.Confirmed
                                 || b.BookingStatus == BookingStatuses.CheckedIn
                                 || b.BookingStatus == BookingStatuses.CheckedOut))
                    .Join(_context.Guests, b => b.GuestId, g => g.Id, (b, g) => g.Email)
                    .ToListAsync();
                var emailMatch = (request.GuestInfo.Email ?? "").Trim();
                var existingConfirmed = guestEmails.Any(e =>
                    string.Equals(e, emailMatch, StringComparison.OrdinalIgnoreCase));
                if (existingConfirmed)
                {
                    throw new InvalidOperationException(
                        "A confirmed booking already exists for these dates. Check your email for the booking confirmation.");
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await GetOrCreateBookingAsync(request);

                var recentCutoff = DateTime.UtcNow.AddMinutes(-10);
                var existingPending = await _context.Payments
                    .Where(p => p.Booking.ListingId == booking.ListingId
                             && p.Booking.CheckinDate == booking.CheckinDate
                             && p.Booking.CheckoutDate == booking.CheckoutDate
                             && p.Status == "pending"
                             && p.RazorpayOrderId != null
                             && p.ReceivedOn > recentCutoff)
                    .OrderByDescending(p => p.ReceivedOn)
                    .FirstOrDefaultAsync();

                if (existingPending is not null)
                {
                    await transaction.CommitAsync();
                    return new RazorpayOrderResponse
                    {
                        KeyId = _keyId,
                        OrderId = existingPending.RazorpayOrderId!,
                        Amount = existingPending.Amount,
                        Currency = booking.Currency,
                        BookingId = existingPending.BookingId
                    };
                }

                var breakdown = await ResolveBreakdownForOrderAsync(request, booking);

                const decimal MaxSaneOrderAmount = 500_000m;
                if (breakdown.FinalAmount <= 0 || breakdown.FinalAmount > MaxSaneOrderAmount)
                    throw new InvalidOperationException(
                        $"Order amount ₹{breakdown.FinalAmount:N0} is outside the allowed range (₹1–₹{MaxSaneOrderAmount:N0}). Please verify listing pricing.");

                booking.PaymentStatus = "pending";
                booking.TotalAmount = breakdown.FinalAmount;
                booking.BaseAmount = breakdown.BaseAmount;
                booking.DiscountAmount = breakdown.DiscountAmount;
                booking.ConvenienceFeeAmount = breakdown.ConvenienceFeeAmount;
                booking.FinalAmount = breakdown.FinalAmount;
                booking.PricingSource = breakdown.PricingSource;
                booking.QuoteTokenNonce = breakdown.QuoteTokenNonce;
                booking.QuoteExpiresAtUtc = breakdown.QuoteExpiresAtUtc;

                // Razorpay expects amount in smallest currency unit (paise); use long to avoid Int32 overflow for large amounts.
                var orderRequest = new
                {
                    amount = (long)(breakdown.FinalAmount * 100),
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
            // Idempotency by RazorpayPaymentId: if we already processed this payment, return early.
            var existingCompleted = await _context.Payments
                .AnyAsync(p => p.RazorpayPaymentId == request.RazorpayPaymentId
                            && PaymentStatuses.Completed == p.Status);
            if (existingCompleted)
            {
                _logger.LogInformation("Idempotent verify: RazorpayPaymentId={PaymentId} already completed.", request.RazorpayPaymentId);
                return true;
            }

            var booking = await _context.Bookings.FindAsync(request.BookingId)
                ?? throw new ArgumentException($"Invalid booking ID: {request.BookingId}");

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == request.BookingId && p.RazorpayOrderId == request.RazorpayOrderId)
                ?? throw new InvalidOperationException("Payment record not found for the given booking and order ID");

            if (PaymentStatuses.IsCompleted(payment.Status))
            {
                _logger.LogInformation("Idempotent verify: payment already completed for BookingId={BookingId}.", request.BookingId);
                return true;
            }

            var text = $"{request.RazorpayOrderId}|{request.RazorpayPaymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
            var computedSignature = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLower();

            if (computedSignature != request.RazorpaySignature.ToLower())
            {
                // Payment failed — delete draft booking + payment to prevent inventory corruption.
                await DeleteDraftBookingAsync(booking, payment);
                return false;
            }

            // Payment succeeded — confirm the booking inside a transaction.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                payment.RazorpayPaymentId = request.RazorpayPaymentId;
                payment.RazorpaySignature = request.RazorpaySignature;
                payment.Status = PaymentStatuses.Completed;
                payment.ReceivedOn = DateTime.UtcNow;
                payment.Note = $"Razorpay Payment ID: {request.RazorpayPaymentId}";

                booking.BookingStatus = BookingStatuses.Confirmed;
                booking.PaymentStatus = "paid";
                booking.AmountReceived = booking.TotalAmount ?? 0;

                // Create AvailabilityBlocks now that payment is confirmed.
                var now = DateTime.UtcNow;
                for (var d = booking.CheckinDate.Date; d < booking.CheckoutDate.Date; d = d.AddDays(1))
                {
                    _context.AvailabilityBlocks.Add(new AvailabilityBlock
                    {
                        ListingId = booking.ListingId,
                        BookingId = booking.Id,
                        StartDate = d,
                        EndDate = d.AddDays(1),
                        BlockType = "Booking",
                        Source = "System",
                        Status = BlockStatuses.Active,
                        Inventory = false,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
                }

                var guest = await _context.Guests.FindAsync(booking.GuestId);
                if (guest != null)
                {
                    // Use guest info from verify request (checkout form at payment time) so Email/WhatsApp use what user entered
                    if (request.GuestInfo != null)
                    {
                        var updated = false;
                        if (!string.IsNullOrWhiteSpace(request.GuestInfo.Phone))
                        {
                            guest.Phone = request.GuestInfo.Phone;
                            updated = true;
                        }
                        if (!string.IsNullOrWhiteSpace(request.GuestInfo.Email))
                        {
                            guest.Email = request.GuestInfo.Email;
                            updated = true;
                        }
                        if (!string.IsNullOrWhiteSpace(request.GuestInfo.Name))
                        {
                            guest.Name = request.GuestInfo.Name;
                            updated = true;
                        }
                        if (updated)
                            await _context.SaveChangesAsync();
                    }

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
                    var outboxNow = DateTime.UtcNow;
                    _context.OutboxMessages.Add(new OutboxMessage
                    {
                        TenantId = booking.TenantId,
                        Topic = "booking.events",
                        EventType = EventTypes.BookingConfirmed,
                        EntityId = booking.Id.ToString(),
                        AggregateId = booking.Id.ToString(),
                        PayloadJson = outboxPayload,
                        CorrelationId = Guid.NewGuid().ToString(),
                        OccurredUtc = outboxNow,
                        SchemaVersion = 1,
                        Status = "Pending",
                        NextAttemptUtc = outboxNow,
                        CreatedAtUtc = outboxNow,
                        UpdatedAtUtc = outboxNow,
                        AttemptCount = 0
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Email sent by ScheduleSenderWorker (Outbox Materializer creates AutomationSchedule, Sender sends)
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>Deletes a PaymentPending draft booking and its associated payment row.
        /// Ensures no AvailabilityBlock rows remain for the booking.</summary>
        private async Task DeleteDraftBookingAsync(Booking booking, Payment payment)
        {
            var blocks = await _context.AvailabilityBlocks
                .Where(ab => ab.BookingId == booking.Id)
                .ToListAsync();
            if (blocks.Count > 0)
                _context.AvailabilityBlocks.RemoveRange(blocks);

            _context.Payments.Remove(payment);
            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted draft booking {BookingId} after payment failure.", booking.Id);
        }

        public async Task<bool> ReconcileWebhookPaymentAsync(string razorpayOrderId, string razorpayPaymentId)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.RazorpayOrderId == razorpayOrderId);

            if (payment == null)
            {
                _logger.LogWarning("Webhook reconcile: no payment found for RazorpayOrderId={OrderId}.", razorpayOrderId);
                return false;
            }

            if (PaymentStatuses.IsCompleted(payment.Status))
            {
                _logger.LogInformation("Webhook reconcile: payment already completed for RazorpayOrderId={OrderId}.", razorpayOrderId);
                return true;
            }

            var booking = await _context.Bookings.FindAsync(payment.BookingId);
            if (booking == null)
            {
                _logger.LogWarning("Webhook reconcile: no booking found for PaymentId={PaymentId}.", payment.Id);
                return false;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                payment.RazorpayPaymentId = razorpayPaymentId;
                payment.Status = PaymentStatuses.Completed;
                payment.ReceivedOn = DateTime.UtcNow;
                payment.Note = $"Reconciled via webhook. Razorpay Payment ID: {razorpayPaymentId}";

                booking.BookingStatus = BookingStatuses.Confirmed;
                booking.PaymentStatus = "paid";
                booking.AmountReceived = booking.TotalAmount ?? 0;

                // Transition existing Hold blocks to Active, or create new blocks for draft bookings.
                var existingBlocks = await _context.AvailabilityBlocks
                    .Where(ab => ab.BookingId == booking.Id)
                    .ToListAsync();
                if (existingBlocks.Count > 0)
                {
                    foreach (var block in existingBlocks)
                    {
                        block.Status = BlockStatuses.Active;
                        block.BlockType = "Booking";
                        block.UpdatedAtUtc = DateTime.UtcNow;
                    }
                }
                else
                {
                    var now = DateTime.UtcNow;
                    for (var d = booking.CheckinDate.Date; d < booking.CheckoutDate.Date; d = d.AddDays(1))
                    {
                        _context.AvailabilityBlocks.Add(new AvailabilityBlock
                        {
                            ListingId = booking.ListingId,
                            BookingId = booking.Id,
                            StartDate = d,
                            EndDate = d.AddDays(1),
                            BlockType = "Booking",
                            Source = "System",
                            Status = BlockStatuses.Active,
                            Inventory = false,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        });
                    }
                }

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
                        occurredAtUtc = DateTime.UtcNow,
                        source = "webhook"
                    });
                    var nowUtc = DateTime.UtcNow;
                    _context.OutboxMessages.Add(new OutboxMessage
                    {
                        TenantId = booking.TenantId,
                        Topic = "booking.events",
                        EventType = EventTypes.BookingConfirmed,
                        EntityId = booking.Id.ToString(),
                        AggregateId = booking.Id.ToString(),
                        PayloadJson = outboxPayload,
                        CorrelationId = Guid.NewGuid().ToString(),
                        OccurredUtc = nowUtc,
                        SchemaVersion = 1,
                        Status = "Pending",
                        NextAttemptUtc = nowUtc,
                        CreatedAtUtc = nowUtc,
                        UpdatedAtUtc = nowUtc,
                        AttemptCount = 0
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Email sent by ScheduleSenderWorker (Outbox Materializer creates AutomationSchedule, Sender sends)
                _logger.LogInformation("Webhook reconcile: payment completed for BookingId={BookingId}, RazorpayOrderId={OrderId}.",
                    booking.Id, razorpayOrderId);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<RazorpayRefundResult> RefundPaymentAsync(int bookingId, decimal amount, string reason)
        {
            var payment = await _context.Payments
                .Where(p => p.BookingId == bookingId
                         && p.RazorpayPaymentId != null
                         && PaymentStatuses.IsCompleted(p.Status))
                .OrderByDescending(p => p.ReceivedOn)
                .FirstOrDefaultAsync();

            if (payment is null)
            {
                return new RazorpayRefundResult
                {
                    Success = false,
                    Error = "No completed Razorpay payment found for this booking."
                };
            }

            if (amount <= 0 || amount > payment.Amount)
            {
                return new RazorpayRefundResult
                {
                    Success = false,
                    Error = $"Refund amount must be between ₹1 and ₹{payment.Amount:N2}."
                };
            }

            var refundRequest = new
            {
                amount = (int)(amount * 100),
                notes = new { reason, bookingId = bookingId.ToString() }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(refundRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"payments/{payment.RazorpayPaymentId}/refund", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Razorpay refund API error for BookingId={BookingId}: {Status} {Body}",
                    bookingId, response.StatusCode, errorBody);
                return new RazorpayRefundResult
                {
                    Success = false,
                    Error = $"Razorpay API error: {response.StatusCode}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var refundResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var refundId = refundResponse.GetProperty("id").GetString();

            var isFullRefund = amount == payment.Amount;
            payment.Status = isFullRefund ? PaymentStatuses.Refunded : PaymentStatuses.PartiallyRefunded;

            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking is not null)
            {
                booking.PaymentStatus = payment.Status;
                if (isFullRefund)
                {
                    booking.AmountReceived = 0;
                }
                else
                {
                    booking.AmountReceived = Math.Max(0, booking.AmountReceived - amount);
                }
            }

            var refundPayment = new Payment
            {
                BookingId = bookingId,
                Amount = -amount,
                Method = "Razorpay",
                Type = "refund",
                ReceivedOn = DateTime.UtcNow,
                RazorpayPaymentId = refundId,
                Status = PaymentStatuses.Completed,
                Note = $"Refund: {reason}"
            };
            _context.Payments.Add(refundPayment);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Refund {RefundId} processed for BookingId={BookingId}, amount={Amount}",
                refundId, bookingId, amount);

            return new RazorpayRefundResult
            {
                Success = true,
                RefundId = refundId,
                Status = payment.Status,
                AmountRefunded = amount
            };
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
            else
            {
                // Update guest Name/Phone from current request so WhatsApp/SMS use the latest number
                if (!string.IsNullOrWhiteSpace(request.GuestInfo.Name))
                    guest.Name = request.GuestInfo.Name;
                if (!string.IsNullOrWhiteSpace(request.GuestInfo.Phone))
                    guest.Phone = request.GuestInfo.Phone;
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
                BookingStatus = BookingStatuses.PaymentPending,
                PaymentStatus = PaymentStatuses.Pending,
                Currency = request.Currency,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return booking;
        }
    }
}
