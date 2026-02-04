using System.Threading.Tasks;
using Atlas.Api.Models.Dtos.Razorpay;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Razorpay order");
                return BadRequest(new { message = ex.Message });
            }
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
