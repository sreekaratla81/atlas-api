using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.DTOs;
using System.Linq;
using System.Collections.Generic;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [Route("bookings")]
    [Produces("application/json")]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingsController> _logger;
        private readonly Atlas.Api.Services.IBookingWorkflowPublisher _bookingWorkflowPublisher;
        private const string ActiveAvailabilityStatus = "Active";
        private const string CancelledAvailabilityStatus = "Cancelled";
        private const string BookingBlockType = "Booking";
        private const string SystemSource = "System";

        public BookingsController(
            AppDbContext context,
            ILogger<BookingsController> logger,
            Atlas.Api.Services.IBookingWorkflowPublisher bookingWorkflowPublisher)
        {
            _context = context;
            _logger = logger;
            _bookingWorkflowPublisher = bookingWorkflowPublisher;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookingListDto>>> GetAll(
            [FromQuery] DateTime? checkinStart,
            [FromQuery] DateTime? checkinEnd,
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
                      BookingSource = b.BookingSource,
                      BookingStatus = b.BookingStatus,
                      TotalAmount = (decimal)b.TotalAmount,
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

        [HttpGet("{id}")]
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
        public async Task<ActionResult<BookingDto>> Create([FromBody] CreateBookingRequest request)
        {
            try
            {
                var listing = await _context.Listings.FindAsync(request.ListingId);
                if (listing == null)
                {
                    ModelState.AddModelError(nameof(request.ListingId), "Listing not found");
                    return BadRequest(ModelState);
                }

                var guest = await _context.Guests.FindAsync(request.GuestId);
                if (guest == null)
                {
                    ModelState.AddModelError(nameof(request.GuestId), "Guest not found");
                    return BadRequest(ModelState);
                }

                var commissionRate = request.BookingSource.ToLower() switch
                {
                    "airbnb" => 0.16m,
                    "booking.com" => 0.15m,
                    "agoda" => 0.18m,
                    _ => 0m
                };

                var commissionAmount = (request.AmountReceived + request.ExtraGuestCharge) * commissionRate;

                var bookingStatus = string.IsNullOrWhiteSpace(request.BookingStatus) ? "Lead" : request.BookingStatus;
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
                    Currency = string.IsNullOrWhiteSpace(request.Currency) ? "INR" : request.Currency,
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

                List<CommunicationLog> communicationLogs = new();
                OutboxMessage? outboxMessage = null;

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

                    (outboxMessage, communicationLogs) = await EnqueueBookingConfirmedWorkflowAsync(booking, guest);
                }

                await _context.SaveChangesAsync();

                if (outboxMessage != null)
                {
                    try
                    {
                        await _bookingWorkflowPublisher.PublishBookingConfirmedAsync(booking, guest, communicationLogs, outboxMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Booking confirmed workflow failed for booking {BookingId}", booking.Id);
                        outboxMessage.AttemptCount += 1;
                        outboxMessage.LastError = ex.Message;

                        foreach (var log in communicationLogs)
                        {
                            log.Status = "Failed";
                            log.AttemptCount += 1;
                            log.LastError = ex.Message;
                        }

                        await _context.SaveChangesAsync();
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
        public async Task<IActionResult> Update(int id, [FromBody] UpdateBookingRequest booking)
        {
            try
            {
                if (booking.Id == 0)
                {
                    booking.Id = id;
                }
                if (id != booking.Id) return BadRequest();

                var existingBooking = await _context.Bookings.FindAsync(id);
                if (existingBooking == null) return NotFound();

                var listing = await _context.Listings.FindAsync(booking.ListingId);
                if (listing == null)
                {
                    ModelState.AddModelError(nameof(booking.ListingId), "Listing not found");
                    return ValidationProblem(ModelState);
                }

                var guest = await _context.Guests.FindAsync(booking.GuestId);
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
                existingBooking.Notes = booking.Notes;
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
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var item = await _context.Bookings.FindAsync(id);
                if (item == null) return NotFound();
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
        public async Task<ActionResult<BookingDto>> Cancel(int id)
        {
            try
            {
                var booking = await _context.Bookings.FindAsync(id);
                if (booking == null)
                {
                    return NotFound();
                }

                if (IsCancelledStatus(booking.BookingStatus) || IsCheckedInStatus(booking.BookingStatus) || IsCheckedOutStatus(booking.BookingStatus))
                {
                    ModelState.AddModelError(nameof(booking.BookingStatus), "Booking cannot be cancelled from its current status.");
                    return BadRequest(ModelState);
                }

                booking.BookingStatus = "Cancelled";
                booking.CancelledAtUtc = DateTime.UtcNow;

                await SyncAvailabilityBlockAsync(booking);
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
        public async Task<ActionResult<BookingDto>> CheckIn(int id)
        {
            try
            {
                var booking = await _context.Bookings.FindAsync(id);
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
        public async Task<ActionResult<BookingDto>> CheckOut(int id)
        {
            try
            {
                var booking = await _context.Bookings.FindAsync(id);
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

                await _context.SaveChangesAsync();

                return Ok(MapToDto(booking));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking out booking {BookingId}", id);
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
                BookingSource = booking.BookingSource,
                BookingStatus = booking.BookingStatus,
                TotalAmount = (decimal)booking.TotalAmount,
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
                Notes = booking.Notes,
                CreatedAt = booking.CreatedAt,
                PaymentStatus = booking.PaymentStatus
            };
        }

        private static bool IsConfirmedStatus(string? status)
        {
            return string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCancelledStatus(string? status)
        {
            return string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCheckedInStatus(string? status)
        {
            return string.Equals(status, "CheckedIn", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCheckedOutStatus(string? status)
        {
            return string.Equals(status, "CheckedOut", StringComparison.OrdinalIgnoreCase);
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

        private async Task<(OutboxMessage? OutboxMessage, List<CommunicationLog> CommunicationLogs)> EnqueueBookingConfirmedWorkflowAsync(Booking booking, Guest guest)
        {
            var templates = await _context.MessageTemplates
                .AsNoTracking()
                .Where(t => t.TemplateKey == "booking-confirmed" && t.IsActive)
                .ToListAsync();

            var (smsTemplate, smsTemplateId) = SelectTemplate("SMS");
            var (whatsAppTemplate, whatsAppTemplateId) = SelectTemplate("WhatsApp");
            var (emailTemplate, emailTemplateId) = SelectTemplate("Email");

            var logs = new List<CommunicationLog>();

            var correlationId = Guid.NewGuid().ToString();
            const string provider = "System";
            const string eventType = "booking-confirmed";

            void AddLog(string channel, string recipient, int? templateId)
            {
                var idempotencyKey = $"{booking.Id}:{channel}:{recipient}:{Guid.NewGuid()}";
                var log = new CommunicationLog
                {
                    Booking = booking,
                    BookingId = booking.Id,
                    GuestId = guest.Id,
                    Channel = channel,
                    EventType = eventType,
                    ToAddress = recipient,
                    TemplateId = templateId,
                    TemplateVersion = templateId.HasValue ? 1 : 0,
                    CorrelationId = correlationId,
                    IdempotencyKey = idempotencyKey,
                    Provider = provider,
                    Status = "Pending",
                    AttemptCount = 0,
                    CreatedAtUtc = DateTime.UtcNow
                };
                logs.Add(log);
                _context.CommunicationLogs.Add(log);
            }

            if (!string.IsNullOrWhiteSpace(guest.Phone))
            {
                AddLog("SMS", guest.Phone, smsTemplateId);
                AddLog("WhatsApp", guest.Phone, whatsAppTemplateId);
            }

            if (!string.IsNullOrWhiteSpace(guest.Email))
            {
                AddLog("Email", guest.Email, emailTemplateId);
            }

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                bookingId = booking.Id,
                guestId = booking.GuestId,
                bookingStatus = booking.BookingStatus,
                occurredAtUtc = DateTime.UtcNow,
                templates = new
                {
                    sms = smsTemplate,
                    whatsApp = whatsAppTemplate,
                    email = emailTemplate
                }
            });

            var outboxMessage = new OutboxMessage
            {
                AggregateType = "Booking",
                AggregateId = booking.Id.ToString(),
                EventType = "booking-confirmed",
                PayloadJson = payload,
                CreatedAtUtc = DateTime.UtcNow,
                AttemptCount = 0
            };
            _context.OutboxMessages.Add(outboxMessage);

            (MessageTemplate? template, int? templateId) SelectTemplate(string channel)
            {
                var template = templates.FirstOrDefault(t => t.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
                return (template, template?.Id);
            }

            return (outboxMessage, logs);
        }
    }
}
