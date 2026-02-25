using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("api/reviews")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReviewsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Guest submits a review after checkout.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewResponseDto>> Create([FromBody] ReviewCreateDto dto)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
            return BadRequest(new { error = "Rating must be between 1 and 5." });

        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId);

        if (booking == null)
            return NotFound(new { error = "Booking not found." });

        if (booking.CheckedOutAtUtc == null)
            return BadRequest(new { error = "Review can only be submitted after checkout." });

        var alreadyReviewed = await _context.Reviews
            .AnyAsync(r => r.BookingId == dto.BookingId);

        if (alreadyReviewed)
            return Conflict(new { error = "A review already exists for this booking." });

        var review = new Review
        {
            BookingId = booking.Id,
            GuestId = booking.GuestId,
            ListingId = booking.ListingId,
            Rating = dto.Rating,
            Title = dto.Title,
            Body = dto.Body
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByListing), new { listingId = review.ListingId }, MapToDto(review));
    }

    /// <summary>Public: returns reviews for a listing with aggregate stats.</summary>
    [HttpGet("/api/listings/{listingId}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ListingReviewsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListingReviewsResponseDto>> GetByListing(int listingId)
    {
        var reviews = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.ListingId == listingId)
            .Include(r => r.Guest)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var response = new ListingReviewsResponseDto
        {
            ListingId = listingId,
            TotalCount = reviews.Count,
            AverageRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 0,
            Reviews = reviews.Select(MapToDto).ToList()
        };

        return Ok(response);
    }

    /// <summary>Host responds to a review.</summary>
    [HttpPut("{id}/response")]
    [Authorize]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponseDto>> RespondToReview(int id, [FromBody] ReviewRespondDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Response))
            return BadRequest(new { error = "Response text is required." });

        var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == id);

        if (review == null)
            return NotFound(new { error = "Review not found." });

        review.HostResponse = dto.Response.Trim();
        review.HostResponseAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(MapToDto(review));
    }

    private static ReviewResponseDto MapToDto(Review r) => new()
    {
        Id = r.Id,
        BookingId = r.BookingId,
        GuestId = r.GuestId,
        GuestName = r.Guest?.Name,
        ListingId = r.ListingId,
        Rating = r.Rating,
        Title = r.Title,
        Body = r.Body,
        CreatedAt = r.CreatedAt,
        HostResponse = r.HostResponse,
        HostResponseAt = r.HostResponseAt
    };
}

public class ReviewCreateDto
{
    public int BookingId { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
}

public class ReviewRespondDto
{
    public string Response { get; set; } = string.Empty;
}

public class ReviewResponseDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public int GuestId { get; set; }
    public string? GuestName { get; set; }
    public int ListingId { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? HostResponse { get; set; }
    public DateTime? HostResponseAt { get; set; }
}

public class ListingReviewsResponseDto
{
    public int ListingId { get; set; }
    public int TotalCount { get; set; }
    public double AverageRating { get; set; }
    public List<ReviewResponseDto> Reviews { get; set; } = new();
}
