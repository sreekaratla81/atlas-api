namespace Atlas.Api.DTOs;

public class AnalyticsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalNightsAvailable { get; set; }
    public int TotalNightsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal OccupancyRate { get; set; }
    public decimal Adr { get; set; }
    public decimal RevPar { get; set; }
    public List<ListingAnalytics> ByListing { get; set; } = new();
}

public class ListingAnalytics
{
    public int ListingId { get; set; }
    public string ListingName { get; set; } = "";
    public int NightsAvailable { get; set; }
    public int NightsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal OccupancyRate { get; set; }
    public decimal Adr { get; set; }
    public decimal RevPar { get; set; }
}

public class MonthlyTrend
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal OccupancyRate { get; set; }
    public decimal Adr { get; set; }
    public decimal RevPar { get; set; }
    public decimal Revenue { get; set; }
    public int BookingsCount { get; set; }
}

public class ChannelPerformance
{
    public string Channel { get; set; } = "";
    public int BookingsCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal Adr { get; set; }
    public decimal SharePercent { get; set; }
}
