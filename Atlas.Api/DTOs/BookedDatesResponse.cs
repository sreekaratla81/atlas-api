namespace Atlas.Api.DTOs
{
    public class BookedDatesResponse
    {
        public Dictionary<int, List<BookedDateRange>> BookedDates { get; set; } = new();
    }
}


