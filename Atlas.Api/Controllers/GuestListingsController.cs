using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/guest/listings")]
    [Produces("application/json")]
    public class GuestListingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStorageUrlBuilder _urlBuilder;
        private readonly ILogger<GuestListingsController> _logger;

        public GuestListingsController(AppDbContext context, IStorageUrlBuilder urlBuilder, ILogger<GuestListingsController> logger)
        {
            _context = context;
            _urlBuilder = urlBuilder;
            _logger = logger;
        }

        [HttpGet]
        [ResponseCache(Duration = 120)]
        public async Task<ActionResult<IEnumerable<PublicListingDto>>> GetAll()
        {
            var listings = await _context.Listings
                .Where(l => l.IsPublic)
                .Include(l => l.Property)
                .Include(l => l.Media)
                .ToListAsync();

            var result = listings.Select(ToDto).ToList();
            Response.Headers["ETag"] = $"W/\"{result.Count}\"";
            _logger.LogInformation("Guest listings fetched {count}", result.Count);
            return Ok(result);
        }

        [HttpGet("{slugOrId}")]
        [ResponseCache(Duration = 120)]
        public async Task<ActionResult<PublicListingDto>> Get(string slugOrId)
        {
            var listing = await FindListing(slugOrId);
            if (listing == null || !listing.IsPublic)
            {
                return NotFound();
            }

            var dto = ToDto(listing);
            Response.Headers["ETag"] = $"W/\"{listing.Id}-{listing.Slug}\"";
            _logger.LogInformation("Guest listing fetched {listingId} {slug}", listing.Id, listing.Slug);
            return Ok(dto);
        }

        [HttpGet("{slugOrId}/availability")]
        [ResponseCache(Duration = 120)]
        public async Task<ActionResult<IEnumerable<DateTime>>> GetAvailability(string slugOrId, [FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            var listing = await FindListing(slugOrId);
            if (listing == null || !listing.IsPublic)
            {
                return NotFound();
            }

            var bookings = await _context.Bookings
                .Where(b => b.ListingId == listing.Id && b.PaymentStatus == "Paid" && b.CheckinDate < to && b.CheckoutDate > from)
                .ToListAsync();

            var dates = new HashSet<DateTime>();
            foreach (var b in bookings)
            {
                var start = b.CheckinDate < from ? from : b.CheckinDate;
                var end = b.CheckoutDate > to ? to : b.CheckoutDate;
                for (var d = start; d < end; d = d.AddDays(1))
                {
                    dates.Add(d.Date);
                }
            }

            var result = dates.OrderBy(d => d).ToList();
            Response.Headers["ETag"] = $"W/\"{listing.Id}-{from:yyyyMMdd}-{to:yyyyMMdd}-{result.Count}\"";
            _logger.LogInformation("Guest listing availability {listingId} {slug} {count} {from} {to}", listing.Id, listing.Slug, result.Count, from, to);
            return Ok(result);
        }

        private async Task<Listing?> FindListing(string slugOrId)
        {
            if (int.TryParse(slugOrId, out var id))
            {
                return await _context.Listings
                    .Include(l => l.Property)
                    .Include(l => l.Media)
                    .FirstOrDefaultAsync(l => l.Id == id);
            }
            var slug = slugOrId.ToLowerInvariant();
            return await _context.Listings
                .Include(l => l.Property)
                .Include(l => l.Media)
                .FirstOrDefaultAsync(l => l.Slug == slug);
        }

        private PublicListingDto ToDto(Listing l)
        {
            var coverBlob = l.CoverImage ?? l.Media.FirstOrDefault(m => m.IsCover)?.BlobName;
            var coverUrl = coverBlob != null ? _urlBuilder.Build(l.BlobContainer, l.BlobPrefix, coverBlob) : null;
            var gallery = l.Media
                .OrderBy(m => m.SortOrder)
                .Take(12)
                .Select(m => _urlBuilder.Build(l.BlobContainer, l.BlobPrefix, m.BlobName))
                .ToList();

            var address = new PublicAddressDto
            {
                Street = l.Property.Address
            };

            return new PublicListingDto
            {
                Id = l.Id,
                Slug = l.Slug,
                Name = l.Name,
                ShortDescription = l.ShortDescription,
                NightlyPrice = l.NightlyPrice,
                CoverImageUrl = coverUrl,
                GalleryUrls = gallery,
                Address = address
            };
        }
    }
}
