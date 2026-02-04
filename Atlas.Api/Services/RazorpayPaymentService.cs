using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Atlas.Api.Data;
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

        public RazorpayPaymentService(
            AppDbContext context, 
            IOptions<RazorpayConfig> config,
            IHttpClientFactory httpClientFactory,
            ILogger<RazorpayPaymentService> logger)
        {
            _context = context;
            _keyId = config.Value.KeyId ?? throw new ArgumentNullException(nameof(config.Value.KeyId));
            _keySecret = config.Value.KeySecret ?? throw new ArgumentNullException(nameof(config.Value.KeySecret));
            _httpClient = httpClientFactory.CreateClient("Razorpay");
            _logger = logger;
            
            // Set up basic auth for Razorpay
            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            _httpClient.BaseAddress = new Uri("https://api.razorpay.com/v1/");
        }

        public async Task<RazorpayOrderResponse> CreateOrderAsync(CreateRazorpayOrderRequest request)
        {
            try
            {
                _logger.LogInformation("Starting to create Razorpay order for request: {Request}", JsonSerializer.Serialize(request));
                
                // Create or update booking
                var booking = await GetOrCreateBookingAsync(request);
                _logger.LogInformation("Booking created/retrieved with ID: {BookingId}", booking.Id);
                
                // Create Razorpay order
                var orderRequest = new
                {
                    amount = (int)(request.Amount * 100), // Convert to paise
                    currency = request.Currency,
                    receipt = $"booking_{booking.Id}",
                    payment_capture = 1 // Auto-capture payment
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(orderRequest),
                    Encoding.UTF8,
                    "application/json");

                _logger.LogInformation("Sending request to Razorpay API");
                var response = await _httpClient.PostAsync("orders", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Razorpay API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    throw new InvalidOperationException($"Razorpay API error: {response.StatusCode} - {errorContent}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Razorpay API response: {Response}", responseContent);
                
                var orderResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var orderId = orderResponse.GetProperty("id").GetString();
                
                if (string.IsNullOrEmpty(orderId))
                {
                    throw new InvalidOperationException("Failed to create Razorpay order: Invalid response from Razorpay");
                }

                // Create payment record
                var payment = new Payment
                {
                    BookingId = booking.Id,
                    Amount = request.Amount,
                    Method = "Razorpay",
                    Type = "payment",
                    ReceivedOn = DateTime.UtcNow,
                    Note = $"Razorpay Order ID: {orderId}",
                    RazorpayOrderId = orderId,
                    Status = "pending"
                };

                _context.Payments.Add(payment);
                
                // Update booking with payment details
                booking.PaymentStatus = "pending";
                booking.TotalAmount = request.Amount;
                booking.Currency = request.Currency;
                
                _logger.LogInformation("Saving booking and payment updates to database");
                await _context.SaveChangesAsync();

                var result = new RazorpayOrderResponse
                {
                    KeyId = _keyId,
                    OrderId = orderId,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    BookingId = booking.Id
                };

                _logger.LogInformation("Successfully created Razorpay order: {OrderId}", orderId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateOrderAsync");
                throw; // Re-throw to be handled by the controller
            }
        }

        public async Task<bool> VerifyAndProcessPaymentAsync(VerifyRazorpayPaymentRequest request)
        {
            _logger.LogInformation("Starting payment verification for booking ID: {BookingId}", request.BookingId);
            
            var booking = await _context.Bookings.FindAsync(request.BookingId);
            if (booking == null)
            {
                _logger.LogError("Booking not found with ID: {BookingId}", request.BookingId);
                throw new ArgumentException($"Invalid booking ID: {request.BookingId}");
            }

            // Find the existing payment record for this booking
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == request.BookingId && p.RazorpayOrderId == request.RazorpayOrderId);

            if (payment == null)
            {
                _logger.LogError("Payment record not found for booking ID: {BookingId} and order ID: {OrderId}", 
                    request.BookingId, request.RazorpayOrderId);
                throw new InvalidOperationException("Payment record not found for the given booking and order ID");
            }

            _logger.LogInformation("Found payment record with ID: {PaymentId}", payment.Id);

            // Verify the payment signature
            var text = $"{request.RazorpayOrderId}|{request.RazorpayPaymentId}";
            var secret = Encoding.UTF8.GetBytes(_keySecret);
            var signature = request.RazorpaySignature;

            using var hmac = new HMACSHA256(secret);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
            var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            if (computedSignature != signature.ToLower())
            {
                _logger.LogWarning("Signature verification failed for booking ID: {BookingId}", request.BookingId);
                booking.PaymentStatus = "failed";
                payment.Status = "failed";
                await _context.SaveChangesAsync();
                return false;
            }

            _logger.LogInformation("Signature verified successfully for booking ID: {BookingId}", request.BookingId);

            // Update the existing payment record with Razorpay details
            payment.RazorpayPaymentId = request.RazorpayPaymentId;
            payment.RazorpaySignature = request.RazorpaySignature;
            payment.Status = "completed";
            payment.ReceivedOn = DateTime.UtcNow;
            payment.Note = $"Razorpay Payment ID: {request.RazorpayPaymentId}";

            // Update booking status
            booking.PaymentStatus = "paid";
            booking.AmountReceived = booking.TotalAmount ?? 0;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully updated payment and booking status for booking ID: {BookingId}", request.BookingId);

            return true;
        }

        private async Task<Booking> GetOrCreateBookingAsync(CreateRazorpayOrderRequest request)
        {
            try
            {
                _logger.LogInformation("Starting GetOrCreateBookingAsync");
                
                if (request.BookingId.HasValue)
                {
                    _logger.LogInformation("Looking for existing booking with ID: {BookingId}", request.BookingId.Value);
                    var existingBooking = await _context.Bookings.FindAsync(request.BookingId.Value);
                    if (existingBooking != null)
                    {
                        _logger.LogInformation("Found existing booking");
                        return existingBooking;
                    }
                    _logger.LogWarning("No booking found with ID: {BookingId}", request.BookingId.Value);
                }

                if (request.BookingDraft == null)
                {
                    throw new ArgumentException("Either bookingId or bookingDraft must be provided");
                }

                _logger.LogInformation("Creating new booking for listing: {ListingId}", request.BookingDraft.ListingId);
                
                // Check if the default guest exists
                const int defaultGuestId = 871; // This should be moved to configuration in a production environment
                var guest = await _context.Guests.FindAsync(defaultGuestId);
                if (guest == null)
                {
                    _logger.LogError("Default guest with ID {DefaultGuestId} not found in the database", defaultGuestId);
                    throw new InvalidOperationException($"Default guest with ID {defaultGuestId} not found in the database. Please ensure there is a guest with this ID.");
                }

                var booking = new Booking
                {
                    ListingId = request.BookingDraft.ListingId,
GuestId = defaultGuestId, // Using default guest ID
                    CheckinDate = request.BookingDraft.CheckinDate,
                    CheckoutDate = request.BookingDraft.CheckoutDate,
                    GuestsPlanned = request.BookingDraft.Guests,
                    Notes = request.BookingDraft.Notes ?? string.Empty,
                    BookingStatus = "Confirmed",
                    PaymentStatus = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Adding new booking to context");
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully created booking with ID: {BookingId}", booking.Id);

                return booking;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateBookingAsync");
                throw; // Re-throw to be handled by the caller
            }
        }
    }
}
