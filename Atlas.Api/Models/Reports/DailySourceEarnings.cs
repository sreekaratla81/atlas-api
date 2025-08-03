using System;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.Api.Models.Reports;

public class CalendarEarningEntry
{
    public DateTime Date { get; set; }
    public List<BookingEarningDetail> Earnings { get; set; } = new();
    public decimal Total => Earnings.Sum(e => e.Amount);
}

public class BookingEarningDetail
{
    public required string Source { get; set; }
    public decimal Amount { get; set; }
}
