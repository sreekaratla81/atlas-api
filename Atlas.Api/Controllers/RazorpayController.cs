using System.Threading.Tasks;
using System.Collections.Generic;
using Atlas.Api.Models.Dtos.Razorpay;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

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
        public async Task<ActionResult<RazorpayOrderResponse>> CreateOrder([FromBody] CreateRazorpayOrderRequest request)
        {
            try
            {
                var response = await _razorpayService.CreateOrderAsync(request);
                return Ok(response);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating Razorpay order");
                var errorMessage = ExtractFullExceptionMessage(dbEx);
                _logger.LogError("Full exception details: {FullException}", dbEx.ToString());
                return BadRequest(new { message = $"Database error: {errorMessage}" });
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
