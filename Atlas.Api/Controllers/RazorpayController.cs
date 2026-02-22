using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Api.Models;
using Atlas.Api.Models.Dtos.Razorpay;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("payments")]
    public class RazorpayController : ControllerBase
    {
        private readonly IRazorpayPaymentService _razorpayService;
        private readonly ILogger<RazorpayController> _logger;
        private readonly string _webhookSecret;

        public RazorpayController(
            IRazorpayPaymentService razorpayService,
            ILogger<RazorpayController> logger,
            IOptions<RazorpayConfig> razorpayConfig)
        {
            _razorpayService = razorpayService;
            _logger = logger;
            _webhookSecret = razorpayConfig.Value.WebhookSecret;
        }

        /// <summary>
        /// Creates a new Razorpay order for payment
        /// </summary>
        [HttpPost("order")]
        [ProducesResponseType(typeof(RazorpayOrderResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        [ProducesResponseType(422)]
        public async Task<ActionResult<RazorpayOrderResponse>> CreateOrder([FromBody] CreateRazorpayOrderRequest request)
        {
            try
            {
                var response = await _razorpayService.CreateOrderAsync(request);
                return Ok(response);
            }
            catch (ArgumentException argEx)
            {
                // Validation errors: missing required fields, invalid listing, etc.
                _logger.LogError(argEx, "Validation error creating Razorpay order");
                var errorMessage = ExtractFullExceptionMessage(argEx);
                return StatusCode(422, new { message = errorMessage });
            }
            catch (ValidationException valEx)
            {
                // Payment validation failed
                _logger.LogError(valEx, "Validation error creating Razorpay order");
                var errorMessage = ExtractFullExceptionMessage(valEx);
                return StatusCode(422, new { message = errorMessage });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating Razorpay order");
                var errorMessage = ExtractFullExceptionMessage(dbEx);
                _logger.LogError("Full exception details: {FullException}", dbEx.ToString());
                
                // Check if it's a unique constraint violation (duplicate order/conflict)
                if (IsUniqueConstraintViolation(dbEx))
                {
                    return StatusCode(409, new { message = "A Razorpay order already exists for this booking. Please use the existing order." });
                }
                
                // Other database validation errors
                return StatusCode(422, new { message = $"Database validation error: {errorMessage}" });
            }
            catch (InvalidOperationException invOpEx)
            {
                _logger.LogError(invOpEx, "Error creating Razorpay order");
                var errorMessage = ExtractFullExceptionMessage(invOpEx);
                _logger.LogError("Full exception details: {FullException}", invOpEx.ToString());
                
                // Check if it's a conflict scenario (booking already confirmed, duplicate order attempt)
                if (errorMessage.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("exists", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(409, new { message = errorMessage });
                }
                
                // Razorpay API errors or other operational errors remain as 400
                return BadRequest(new { message = errorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Razorpay order");
                var errorMessage = ExtractFullExceptionMessage(ex);
                _logger.LogError("Full exception details: {FullException}", ex.ToString());
                return BadRequest(new { message = errorMessage });
            }
        }

        private string ExtractFullExceptionMessage(Exception ex)
        {
            var messages = new List<string>();
            var current = ex;
            
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    messages.Add(current.Message);
                }
                current = current.InnerException;
            }
            
            return string.Join(" -> ", messages);
        }

        /// <summary>
        /// Checks if a DbUpdateException is caused by a unique constraint violation (conflict scenario)
        /// </summary>
        private bool IsUniqueConstraintViolation(DbUpdateException dbEx)
        {
            // Check the full exception message chain for unique constraint violation indicators
            var fullMessage = ExtractFullExceptionMessage(dbEx);
            
            // Check for SQL Server unique constraint violation indicators
            if (fullMessage.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("cannot insert duplicate", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("Violation of UNIQUE KEY", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("2627", StringComparison.OrdinalIgnoreCase) || // SQL Server error code for UNIQUE constraint
                fullMessage.Contains("2601", StringComparison.OrdinalIgnoreCase))   // SQL Server error code for duplicate key
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Verifies and processes a Razorpay payment.
        /// Idempotent: repeated calls with the same already-completed payment return 200 without side effects.
        /// </summary>
        [HttpPost("verify")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyRazorpayPaymentRequest request)
        {
            try
            {
                var result = await _razorpayService.VerifyAndProcessPaymentAsync(request);
                if (result)
                {
                    return Ok(new { success = true, message = "Payment verified and processed successfully" });
                }
                return BadRequest(new { success = false, message = "Payment verification failed" });
            }
            catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
            {
                _logger.LogWarning(dbEx, "Duplicate RazorpayOrderId detected during verify for BookingId={BookingId}", request.BookingId);
                return StatusCode(409, new { success = false, message = "A payment with this Razorpay order ID already exists." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Razorpay payment");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Razorpay server-to-server webhook for payment reconciliation.
        /// Catches payments where the client dropped before calling /verify.
        /// Validates webhook signature using HMAC-SHA256 with webhook secret.
        /// </summary>
        [HttpPost("webhook")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Webhook()
        {
            if (string.IsNullOrWhiteSpace(_webhookSecret))
            {
                _logger.LogWarning("Razorpay webhook received but WebhookSecret is not configured; ignoring.");
                return Ok(new { status = "ignored", reason = "webhook_secret_not_configured" });
            }

            string body;
            using (var reader = new System.IO.StreamReader(Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            var signature = Request.Headers["X-Razorpay-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(signature))
            {
                _logger.LogWarning("Razorpay webhook missing X-Razorpay-Signature header.");
                return BadRequest(new { status = "error", reason = "missing_signature" });
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookSecret));
            var expected = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)))
                .Replace("-", "").ToLower();
            if (!string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Razorpay webhook signature mismatch.");
                return BadRequest(new { status = "error", reason = "invalid_signature" });
            }

            RazorpayWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<RazorpayWebhookPayload>(body);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Razorpay webhook payload deserialization failed.");
                return BadRequest(new { status = "error", reason = "invalid_payload" });
            }

            if (payload?.Event != "payment.captured" && payload?.Event != "payment.authorized")
            {
                _logger.LogInformation("Razorpay webhook event {Event} ignored (not payment.captured/authorized).", payload?.Event);
                return Ok(new { status = "ignored", @event = payload?.Event });
            }

            var paymentEntity = payload.Payload?.Payment?.Entity;
            if (paymentEntity == null || string.IsNullOrEmpty(paymentEntity.OrderId))
            {
                _logger.LogWarning("Razorpay webhook payload missing payment entity or order_id.");
                return Ok(new { status = "ignored", reason = "missing_payment_entity" });
            }

            try
            {
                var reconciled = await _razorpayService.ReconcileWebhookPaymentAsync(
                    paymentEntity.OrderId, paymentEntity.Id);

                return Ok(new { status = reconciled ? "reconciled" : "no_match", orderId = paymentEntity.OrderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook reconciliation failed for OrderId={OrderId}.", paymentEntity.OrderId);
                return StatusCode(500, new { status = "error", reason = "reconciliation_failed" });
            }
        }
    }
}
