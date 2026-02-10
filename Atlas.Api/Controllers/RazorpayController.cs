using System.Threading.Tasks;
using System.Collections.Generic;
using Atlas.Api.Models.Dtos.Razorpay;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RazorpayController : ControllerBase
    {
        private readonly IRazorpayPaymentService _razorpayService;
        private readonly ILogger<RazorpayController> _logger;

        public RazorpayController(
            IRazorpayPaymentService razorpayService,
            ILogger<RazorpayController> logger)
        {
            _razorpayService = razorpayService;
            _logger = logger;
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
        /// Verifies and processes a Razorpay payment
        /// </summary>
        [HttpPost("verify")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Razorpay payment");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
