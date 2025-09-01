using System;
using System.Linq;
using System.Diagnostics;
using Application.Guests.Queries.SearchGuests;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Infrastructure.Phone;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Guests.Queries.SearchGuests;

public class SearchGuestsQueryHandler
{
    private readonly AppDbContext _context;
    private readonly PhoneNormalizer _phoneNormalizer;
    private readonly ILogger<SearchGuestsQueryHandler> _logger;

    public SearchGuestsQueryHandler(AppDbContext context, PhoneNormalizer phoneNormalizer, ILogger<SearchGuestsQueryHandler> logger)
    {
        _context = context;
        _phoneNormalizer = phoneNormalizer;
        _logger = logger;
    }

    public async Task<SearchGuestsResponse> Handle(SearchGuestsRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var q = (request.Query ?? string.Empty).Trim();
        var digits = new string(q.Where(char.IsDigit).ToArray());
        var isPhone = digits.Length == q.Length && digits.Length > 0;
        var normalizedPhone = isPhone ? _phoneNormalizer.Normalize(digits) : null;
        var lowerEmail = q.ToLowerInvariant();
        var isEmail = q.Contains('@');
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize;

        var baseQuery = _context.Guests.AsNoTracking().AsQueryable();

        if (isPhone && normalizedPhone != null)
        {
            baseQuery = baseQuery.Where(g => g.PhoneE164 == normalizedPhone);
        }
        else if (isEmail)
        {
            baseQuery = baseQuery.Where(g => g.Email.ToLower() == lowerEmail);
        }
        else
        {
            var lower = q.ToLowerInvariant();
            baseQuery = baseQuery.Where(g => g.NameSearch.Contains(lower));
        }

        var ordered = baseQuery
            .OrderByDescending(g => isPhone && g.PhoneE164 == normalizedPhone)
            .ThenByDescending(g => isEmail && g.Email.ToLower() == lowerEmail)
            .ThenByDescending(g => g.NameSearch.StartsWith(q.ToLowerInvariant()))
            .ThenBy(g => g.Name);

        int total;
        if (page <= 2)
        {
            total = await ordered.CountAsync(cancellationToken);
        }
        else
        {
            total = Math.Min(1000, await ordered.Take(1000).CountAsync(cancellationToken));
        }

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GuestListItem(g.Id, g.Name, g.Phone, g.Email, g.PhoneE164))
            .ToListAsync(cancellationToken);

        sw.Stop();
        _logger.LogInformation("SearchGuests {UserId} {qLen} {isPhone} {isEmail} {page} {pageSize} {resultCount} {elapsedMs}",
            "anonymous", q.Length, isPhone, isEmail, page, pageSize, items.Count, sw.ElapsedMilliseconds);

        return new SearchGuestsResponse(total, page, pageSize, items);
    }
}
