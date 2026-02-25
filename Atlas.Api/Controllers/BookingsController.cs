using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Events;
using Atlas.Api.Models;
using Atlas.Api.Services.Billing;
using Atlas.Api.Services.Scheduling;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Controllers
{
    /// <summary>Manages booking lifecycle including create, update, cancel, checkin, checkout.</summary>
    [ApiController]
    [Route("bookings")]
    [Produces("application/json")]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingsController> _logger;
#pragma warning disable CS0618 // BL-011: retained for planned workflow integration
        private readonly Atlas.Api.Services.IBookingWorkflowPublisher _bookingWorkflowPublisher;
#pragma warning restore CS0618
        private readonly CreditsService _credits;
        private readonly ITenantContextAccessor _tenantAccessor;
        private readonly BookingScheduleService _scheduleService;
        private readonly Atlas.Api.Services.IRazorpayPaymentService _razorpayService;
        private readonly Atlas.Api.Services.IInvoiceService _invoiceService;
        private const string ActiveAvailabilityStatus = BlockStatuses.Active;
        private const string CancelledAvailabilityStatus = BookingStatuses.Cancelled;
        private const string BookingBlockType = "Booking";
        private const string SystemSource = "System";

        public BookingsController(
            AppDbContext context,
            ILogger<BookingsController> logger,
#pragma warning disable CS0618
            Atlas.Api.Services.IBookingWorkflowPublisher bookingWorkflowPublisher,
#pragma warning restore CS0618
            CreditsService credits,
            ITenantContextAccessor tenantAccessor,
            BookingScheduleService scheduleService,
            Atlas.Api.Services.IRazorpayPaymentService razorpayService,
            Atlas.Api.Services.IInvoiceService invoiceService)
        {
            _context = context;
            _logger = logger;
            _bookingWorkflowPublisher = bookingWorkflowPublisher;
            _credits = credits;
            _tenantAccessor = tenantAccessor;
            _scheduleService = scheduleService;
            _razorpayService = razorpayService;
            _invoiceService = invoiceService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<BookingListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<BookingListDto>>> GetAll(
            [FromQuery] DateTime? checkinStart,
            [FromQuery] DateTime? checkinEnd,
            [FromQuery] int? listingId,
            [FromQuery] int? bookingId,
            [FromQuery] string? include)
        {
            try
            {
                var query = _context.Bookings
                    .AsNoTracking()
                    .AsQueryable();

                if (checkinStart.HasValue)
                    query = query.Where(b => b.CheckinDate >= checkinStart.Value);

                if (checkinEnd.HasValue)
                    query = query.Where(b => b.CheckinDate <= checkinEnd.Value);

                if (listingId.HasValue)
                    query = query.Where(b => b.ListingId == listingId.Value);

                if (bookingId.HasValue)
                    query = query.Where(b => b.Id == bookingId.Value);

                var includes = (include ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var includeGuest = includes.Contains("guest", StringComparer.OrdinalIgnoreCase);

                var bookings = await query
                  .Select(b => new BookingListDto
                  {
                      Id = b.Id,
                      GuestId = b.GuestId,
                      BankAccountId = b.BankAccountId,
                      Listing = _context.Listings
                                    .Where(l => l.Id == b.ListingId)
                                    .Select(l => l.Name)
                                    .FirstOrDefault(),
                      Guest = _context.Guests
                                    .Where(g => g.Id == b.GuestId)
                                    .Select(g => g.Name + " " + g.Phone)
                                    .FirstOrDefault(),
                      CheckinDate = b.CheckinDate,
                      CheckoutDate = b.CheckoutDate,
                      BookingSource = b.BookingSource ?? string.Empty,
                      BookingStatus = b.BookingStatus,
                      TotalAmount = b.TotalAmount ?? 0m,
                      Currency = b.Currency,
                      ExternalReservationId = b.ExternalReservationId,
                      ConfirmationSentAtUtc = b.ConfirmationSentAtUtc,
                      RefundFreeUntilUtc = b.RefundFreeUntilUtc,
                      CheckedInAtUtc = b.CheckedInAtUtc,
                      CheckedOutAtUtc = b.CheckedOutAtUtc,
                      CancelledAtUtc = b.CancelledAtUtc,
                      AmountReceived = b.AmountReceived,
                      GuestsPlanned = b.GuestsPlanned ?? 0,
                      GuestsActual = b.GuestsActual ?? 0,
                      ExtraGuestCharge = b.ExtraGuestCharge ?? 0,
                      CommissionAmount = b.CommissionAmount ?? 0,
                      Notes = b.Notes,
                      CreatedAt = b.CreatedAt,
                      PaymentStatus = b.PaymentStatus,
                      BankAccount = _context.BankAccounts
                                    .Where(ba => ba.Id == b.BankAccountId)
                                    .Select(ba => ba.BankName + " - " + (ba.AccountNumber.Length >= 4 ? ba.AccountNumber.Substring(ba.AccountNumber.Length - 4) : ba.AccountNumber))
                                    .FirstOrDefault(),
                  })
                  .ToListAsync();

                return Ok(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bookings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("by-reference")]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BookingDto>> GetByExternalReservationId([FromQuery] string? externalReservationId)
        {
            if (string.IsNullOrWhiteSpace(externalReservationId))
                return BadRequest(new { error = "externalReservationId is required." });
            try
            {
                var item = await _context.Bookings
                    .AsNoTracking()
                    .Include(b => b.Listing)
                    .Include(b => b.Guest)
                    .FirstOrDefaultAsync(b => b.ExternalReservationId == externalReservationId);
                return item == null ? NotFound() : Ok(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving booking by externalReservationId");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BookingDto>> Get(int id)
        {
            try
            {
                var item = await _context.Bookings
                    .Include(b => b.Listing)
                    .Include(b => b.Guest)
                    .FirstOrDefaultAsync(b => b.Id == id);
                return item == null ? NotFound() : Ok(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BookingDto>> Create([FromBody] CreateBookingRequest request)
        {
            try
            {
                var listing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == request.ListingId);
                if (listing == null)
                {
                    ModelState.AddModelError(nameof(request.ListingId), "Listing not found");
                    return BadRequest(ModelState);
                }

                var guest = await _context.Guests.FirstOrDefaultAsync(x => x.Id == request.GuestId);
                if (guest == null)
                {
                    ModelState.AddModelError(nameof(request.GuestId), "Guest not found");
                    return BadRequest(ModelState);
                }

                var commissionRate = CommissionRates.ForSource(request.BookingSource);

                var commissionAmount = (request.AmountReceived + request.ExtraGuestCharge) * commissionRate;

                var bookingStatus = string.IsNullOrWhiteSpace(request.BookingStatus) ? BookingStatuses.Lead : request.BookingStatus;
                if (IsConfirmedStatus(bookingStatus))
                {
                    var hasOverlap = await HasActiveOverlapAsync(listing.Id, request.CheckinDate, request.CheckoutDate, null);
                    if (hasOverlap)
                    {
                        ModelState.AddModelError(nameof(request.CheckinDate), "Booking dates overlap an existing confirmed booking.");
                        return BadRequest(ModelState);
                    }
                }

                var booking = new Booking
                {
                    ListingId = listing.Id,
                    Listing = listing,
                    GuestId = guest.Id,
                    Guest = guest,
                    BookingSource = request.BookingSource,
                    BookingStatus = bookingStatus,
                    TotalAmount = request.TotalAmount ?? 0m,
                    Currency = string.IsNullOrWhiteSpace(request.Currency) ? CurrencyConstants.INR : request.Currency,
                    ExternalReservationId = request.ExternalReservationId,
                    ConfirmationSentAtUtc = request.ConfirmationSentAtUtc,
                    RefundFreeUntilUtc = request.RefundFreeUntilUtc,
                    CheckedInAtUtc = request.CheckedInAtUtc,
                    CheckedOutAtUtc = request.CheckedOutAtUtc,
                    CancelledAtUtc = request.CancelledAtUtc,
                    PaymentStatus = string.IsNullOrWhiteSpace(request.PaymentStatus) ? "Paid" : request.PaymentStatus,
                    CheckinDate = request.CheckinDate,
                    CheckoutDate = request.CheckoutDate,
                    AmountReceived = request.AmountReceived,
                    BankAccountId = request.BankAccountId,
                    GuestsPlanned = request.GuestsPlanned,
                    GuestsActual = request.GuestsActual,
                    ExtraGuestCharge = request.ExtraGuestCharge,
                    CommissionAmount = commissionAmount,
                    Notes = request.Notes ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Bookings.Add(booking);

                if (IsConfirmedStatus(booking.BookingStatus))
                {
                    _context.AvailabilityBlocks.Add(new AvailabilityBlock
                    {
                        ListingId = booking.ListingId,
                        Booking = booking,
                        BookingId = booking.Id,
                        StartDate = booking.CheckinDate,
                        EndDate = booking.CheckoutDate,
                        BlockType = BookingBlockType,
                        Source = SystemSource,
                        Status = ActiveAvailabilityStatus,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    });

                    AddBookingConfirmedOutbox(booking, guest);
                    _scheduleService.CreateSchedulesForConfirmedBooking(booking);
                }

                AddBookingCreatedOutbox(booking, guest);

                await _context.SaveChangesAsync();

                var tenantId = _tenantAccessor.TenantId ?? 0;
                if (tenantId > 0)
                {
                    try
                    {
                        await _credits.DebitForBookingAsync(tenantId, booking.Id);
                    }
                    catch (TenantLockedException)
                    {
                        _logger.LogWarning("Tenant {TenantId} credits exhausted after booking {BookingId}. Booking allowed, tenant now locked.", tenantId, booking.Id);
                    }
                }

                var dto = MapToDto(booking);
                return CreatedAtAction(nameof(Get), new { id = booking.Id }, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateBookingRequest booking)
        {
            try
            {
                if (booking.Id == 0)
                {
                    booking.Id = id;
                }
                if (id != booking.Id) return BadRequest();

                var existingBooking = await _context.Bookings.FirstOrDefaultAsync(x => x.Id == id);
                if (existingBooking == null) return NotFound();

                var listing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == booking.ListingId);
                if (listing == null)
                {
                    ModelState.AddModelError(nameof(booking.ListingId), "Listing not found");
                    return ValidationProblem(ModelState);
                }

                var guest = await _context.Guests.FirstOrDefaultAsync(x => x.Id == booking.GuestId);
                if (guest == null)
                {
                    ModelState.AddModelError(nameof(booking.GuestId), "Guest not found");
                    return ValidationProblem(ModelState);
                }

                // Update allowed fields
                existingBooking.GuestId = guest.Id;
                existingBooking.Guest = guest;
                existingBooking.ListingId = listing.Id;
                existingBooking.Listing = listing;
                existingBooking.CheckinDate = booking.CheckinDate;
                existingBooking.CheckoutDate = booking.CheckoutDate;
                existingBooking.BookingSource = booking.BookingSource;
                if (!string.IsNullOrWhiteSpace(booking.BookingStatus))
                {
                    existingBooking.BookingStatus = booking.BookingStatus;
                }
                if (booking.TotalAmount.HasValue)
                {
                    existingBooking.TotalAmount = booking.TotalAmount.Value;
                }
                if (!string.IsNullOrWhiteSpace(booking.Currency))
                {
                    existingBooking.Currency = booking.Currency;
                }
                if (booking.ExternalReservationId != null)
                {
                    existingBooking.ExternalReservationId = booking.ExternalReservationId;
                }
                if (booking.ConfirmationSentAtUtc.HasValue)
                {
                    existingBooking.ConfirmationSentAtUtc = booking.ConfirmationSentAtUtc;
                }
                if (booking.RefundFreeUntilUtc.HasValue)
                {
                    existingBooking.RefundFreeUntilUtc = booking.RefundFreeUntilUtc;
                }
                if (booking.CheckedInAtUtc.HasValue)
                {
                    existingBooking.CheckedInAtUtc = booking.CheckedInAtUtc;
                }
                if (booking.CheckedOutAtUtc.HasValue)
                {
                    existingBooking.CheckedOutAtUtc = booking.CheckedOutAtUtc;
                }
                if (booking.CancelledAtUtc.HasValue)
                {
                    existingBooking.CancelledAtUtc = booking.CancelledAtUtc;
                }
                if (!string.IsNullOrWhiteSpace(booking.PaymentStatus))
                {
                    existingBooking.PaymentStatus = booking.PaymentStatus;
                }
                existingBooking.AmountReceived = booking.AmountReceived;
                existingBooking.GuestsPlanned = booking.GuestsPlanned;
                existingBooking.GuestsActual = booking.GuestsActual;
                existingBooking.ExtraGuestCharge = booking.ExtraGuestCharge;
                existingBooking.CommissionAmount = booking.CommissionAmount;
                existingBooking.Notes = booking.Notes ?? existingBooking.Notes ?? string.Empty;
                existingBooking.BankAccountId = booking.BankAccountId;

                if (IsConfirmedStatus(existingBooking.BookingStatus))
                {
                    var hasOverlap = await HasActiveOverlapAsync(existingBooking.ListingId, existingBooking.CheckinDate, existingBooking.CheckoutDate, existingBooking.Id);
                    if (hasOverlap)
                    {
                        ModelState.AddModelError(nameof(booking.CheckinDate), "Booking dates overlap an existing confirmed booking.");
                        return ValidationProblem(ModelState);
                    }
                }

                await SyncAvailabilityBlockAsync(existingBooking);

                if (IsConfirmedStatus(existingBooking.BookingStatus))
                {
                    var guestForConfirm = await _context.Guests.AsNoTracking().FirstOrDefaultAsync(g => g.Id == existingBooking.GuestId);
                    if (guestForConfirm != null)
                        AddBookingConfirmedOutbox(existingBooking, guestForConfirm);
                    _scheduleService.CreateSchedulesForConfirmedBooking(existingBooking);
                }
                else if (IsCancelledStatus(existingBooking.BookingStatus))
                {
                    var guestForCancel = await _context.Guests.AsNoTracking().FirstOrDefaultAsync(g => g.Id == existingBooking.GuestId);
                    AddOutboxMessage("booking.events", EventTypes.BookingCancelled, existingBooking.Id.ToString(),
                        new { bookingId = existingBooking.Id, guestId = existingBooking.GuestId, cancelledAtUtc = existingBooking.CancelledAtUtc,
                              guest = guestForCancel != null ? new { guestForCancel.Phone, guestForCancel.Email } : (object?)null });
                    await _scheduleService.CancelSchedulesForBookingAsync(existingBooking.Id);
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency error updating booking {BookingId}", id);
                    return StatusCode(500, "A concurrency error occurred while updating the booking.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var item = await _context.Bookings.FirstOrDefaultAsync(x => x.Id == id);
                if (item == null) return NotFound();

                var blocks = await _context.AvailabilityBlocks
                    .Where(ab => ab.BookingId == id)
                    .ToListAsync();
                if (blocks.Count > 0)
                    _context.AvailabilityBlocks.RemoveRange(blocks);

                _context.Bookings.Remove(item);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/cancel")]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BookingDto>> Cancel(int id)
        {
            try
            {
                var booking = await _context.Bookings.FirstOrDefaultAsync(x => x.Id == id);
                if (booking == null)
                {
                    return NotFound();
                }

                if (IsCancelledStatus(booking.BookingStatus) || IsCheckedInStatus(booking.BookingStatus) || IsCheckedOutStatus(booking.BookingStatus))
                {
                    ModelState.AddModelError(nameof(booking.BookingStatus), "Booking cannot be cancelled from its current status.");
                    return BadRequest(ModelState);
                }

                booking.BookingStatus = BookingStatuses.Cancelled;
                booking.CancelledAtUtc = DateTime.UtcNow;

                await SyncAvailabilityBlockAsync(booking);
                await _scheduleService.CancelSchedulesForBookingAsync(booking.Id);
                var guestForCancel = await _context.Guests.AsNoTracking().FirstOrDefaultAsync(g => g.Id == booking.GuestId);
                AddOutboxMessage("booking.events", EventTypes.BookingCancelled, booking.Id.ToString(), new { bookingId = booking.Id, guestId = booking.GuestId, cancelledAtUtc = booking.CancelledAtUtc, guest = guestForCancel != null ? new { guestForCancel.Phone, guestForCancel.Email } : (object?)null });
                await _context.SaveChangesAsync();

                return Ok(MapToDto(booking));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/checkin")]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BookingDto>> CheckIn(int id)
        {
            try
            {
                var booking = await _context.Bookings.FirstOrDefaultAsync(x => x.Id == id);
                if (booking == null)
                {
                    return NotFound();
                }

                if (!IsConfirmedStatus(booking.BookingStatus))
                {
                    ModelState.AddModelError(nameof(booking.BookingStatus), "Booking must be confirmed before check-in.");
                    return BadRequest(ModelState);
                }

                booking.BookingStatus = "CheckedIn";
                booking.CheckedInAtUtc = DateTime.UtcNow;

                AddOutboxMessage("stay.events", EventTypes.StayCheckedIn, booking.Id.ToString(), new { bookingId = booking.Id, listingId = booking.ListingId, checkedInAtUtc = DateTime.UtcNow });
                await _context.SaveChangesAsync();

                return Ok(MapToDto(booking));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking in booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/checkout")]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BookingDto>> CheckOut(int id)
        {
            try
            {
                var booking = await _context.Bookings.FirstOrDefaultAsync(x => x.Id == id);
                if (booking == null)
                {
                    return NotFound();
                }

                if (!IsCheckedInStatus(booking.BookingStatus))
                {
                    ModelState.AddModelError(nameof(booking.BookingStatus), "Booking must be checked in before checkout.");
                    return BadRequest(ModelState);
                }

                booking.BookingStatus = "CheckedOut";
                booking.CheckedOutAtUtc = DateTime.UtcNow;

                AddOutboxMessage("stay.events", EventTypes.StayCheckedOut, booking.Id.ToString(), new { bookingId = booking.Id, listingId = booking.ListingId, checkedOutAtUtc = DateTime.UtcNow });
                await _context.SaveChangesAsync();

                return Ok(MapToDto(booking));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking out booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        public class RefundRequest
        {
            public decimal Amount { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        [HttpPost("{id}/refund")]
        [ProducesResponseType(typeof(Atlas.Api.Services.RazorpayRefundResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Refund(int id, [FromBody] RefundRequest request)
        {
            try
            {
                var booking = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (booking == null) return NotFound();

                if (request.Amount <= 0)
                    return BadRequest(new { message = "Refund amount must be greater than zero." });

                var result = await _razorpayService.RefundPaymentAsync(id, request.Amount, request.Reason);
                if (!result.Success)
                    return BadRequest(new { message = result.Error });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for BookingId={BookingId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id}/invoice")]
        [ProducesResponseType(typeof(BookingInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrGenerateInvoice(int id, CancellationToken ct)
        {
            try
            {
                var booking = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
                if (booking is null) return NotFound(new { error = "Booking not found." });

                var invoice = await _invoiceService.GetInvoiceByBookingIdAsync(id, ct)
                              ?? await _invoiceService.GenerateInvoiceAsync(id, ct);

                return Ok(invoice);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invoice generation failed for booking {BookingId}", id);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice for booking {BookingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private static BookingDto MapToDto(Booking booking)
        {
            return new BookingDto
            {
                Id = booking.Id,
                ListingId = booking.ListingId,
                GuestId = booking.GuestId,
                CheckinDate = booking.CheckinDate,
                CheckoutDate = booking.CheckoutDate,
                BookingSource = booking.BookingSource ?? string.Empty,
                BookingStatus = booking.BookingStatus,
                TotalAmount = booking.TotalAmount ?? 0m,
                Currency = booking.Currency,
                ExternalReservationId = booking.ExternalReservationId,
                ConfirmationSentAtUtc = booking.ConfirmationSentAtUtc,
                RefundFreeUntilUtc = booking.RefundFreeUntilUtc,
                CheckedInAtUtc = booking.CheckedInAtUtc,
                CheckedOutAtUtc = booking.CheckedOutAtUtc,
                CancelledAtUtc = booking.CancelledAtUtc,
                AmountReceived = booking.AmountReceived,
                BankAccountId = booking.BankAccountId,
                GuestsPlanned = booking.GuestsPlanned ?? 0,
                GuestsActual = booking.GuestsActual ?? 0,
                ExtraGuestCharge = booking.ExtraGuestCharge ?? 0,
                CommissionAmount = booking.CommissionAmount ?? 0,
                Notes = booking.Notes ?? string.Empty,
                CreatedAt = booking.CreatedAt,
                PaymentStatus = booking.PaymentStatus
            };
        }

        private static bool IsConfirmedStatus(string? status)
        {
            return BookingStatuses.IsConfirmed(status ?? "");
        }

        private static bool IsCancelledStatus(string? status)
        {
            return BookingStatuses.IsCancelled(status ?? "");
        }

        private static bool IsCheckedInStatus(string? status)
        {
            return string.Equals(status, BookingStatuses.CheckedIn, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCheckedOutStatus(string? status)
        {
            return string.Equals(status, BookingStatuses.CheckedOut, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> HasActiveOverlapAsync(int listingId, DateTime startDate, DateTime endDate, int? bookingId)
        {
            return await _context.AvailabilityBlocks
                .AsNoTracking()
                .AnyAsync(block => block.ListingId == listingId
                    && block.Status == ActiveAvailabilityStatus
                    && block.StartDate < endDate
                    && block.EndDate > startDate
                    && (bookingId == null || block.BookingId != bookingId));
        }

        private async Task SyncAvailabilityBlockAsync(Booking booking)
        {
            if (!IsConfirmedStatus(booking.BookingStatus) && !IsCancelledStatus(booking.BookingStatus))
            {
                return;
            }

            var existingBlock = await _context.AvailabilityBlocks
                .FirstOrDefaultAsync(block => block.BookingId == booking.Id);

            if (IsConfirmedStatus(booking.BookingStatus))
            {
                if (existingBlock == null)
                {
                    _context.AvailabilityBlocks.Add(new AvailabilityBlock
                    {
                        ListingId = booking.ListingId,
                        BookingId = booking.Id,
                        StartDate = booking.CheckinDate,
                        EndDate = booking.CheckoutDate,
                        BlockType = BookingBlockType,
                        Source = SystemSource,
                        Status = ActiveAvailabilityStatus,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                    return;
                }

                existingBlock.ListingId = booking.ListingId;
                existingBlock.StartDate = booking.CheckinDate;
                existingBlock.EndDate = booking.CheckoutDate;
                existingBlock.BlockType = BookingBlockType;
                existingBlock.Source = SystemSource;
                existingBlock.Status = ActiveAvailabilityStatus;
                existingBlock.UpdatedAtUtc = DateTime.UtcNow;
                return;
            }

            if (IsCancelledStatus(booking.BookingStatus) && existingBlock != null)
            {
                existingBlock.Status = CancelledAvailabilityStatus;
                existingBlock.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        private void AddOutboxMessage(string topic, string eventType, string entityId, object payload, string? correlationId = null)
        {
            var correlation = correlationId ?? Guid.NewGuid().ToString();
            var payloadJson = JsonSerializer.Serialize(payload);
            var row = new OutboxMessage
            {
                Topic = topic,
                EventType = eventType,
                EntityId = entityId,
                PayloadJson = payloadJson,
                CorrelationId = correlation,
                OccurredUtc = DateTime.UtcNow,
                SchemaVersion = 1,
                Status = OutboxStatuses.Pending,
                NextAttemptUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                AttemptCount = 0
            };
            _context.OutboxMessages.Add(row);
        }

        private void AddBookingConfirmedOutbox(Booking booking, Guest guest)
        {
            var payload = new
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
            };
            AddOutboxMessage("booking.events", EventTypes.BookingConfirmed, booking.Id.ToString(), payload);
        }

        private void AddBookingCreatedOutbox(Booking booking, Guest guest)
        {
            var payload = new
            {
                bookingId = booking.Id,
                guestId = guest.Id,
                listingId = booking.ListingId,
                bookingSource = booking.BookingSource,
                bookingStatus = booking.BookingStatus,
                checkinDate = booking.CheckinDate,
                checkoutDate = booking.CheckoutDate,
                guestPhone = guest.Phone,
                guestEmail = guest.Email,
                occurredAtUtc = DateTime.UtcNow
            };
            AddOutboxMessage("booking.events", EventTypes.BookingCreated, booking.Id.ToString(), payload);
        }
    }
}
