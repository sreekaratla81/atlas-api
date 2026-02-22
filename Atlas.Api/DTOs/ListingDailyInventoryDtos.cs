using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs
{
    /// <summary>Single daily inventory cell for calendar display.</summary>
    public class ListingDailyInventoryCalendarCellDto
    {
        public DateTime Date { get; set; }
        public int RoomsAvailable { get; set; }
        public string Source { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    /// <summary>Request body for bulk-upserting daily inventory for a listing.</summary>
    public class ListingDailyInventoryBulkUpsertRequestDto
    {
        [Required]
        public int ListingId { get; set; }

        [Required]
        [MinLength(1)]
        public List<ListingDailyInventoryBulkUpsertItemDto> Items { get; set; } = new();
    }

    /// <summary>Single item within a daily inventory bulk upsert request.</summary>
    public class ListingDailyInventoryBulkUpsertItemDto
    {
        [Required]
        public DateTime Date { get; set; }

        [Range(0, int.MaxValue)]
        public int RoomsAvailable { get; set; }

        [Required]
        [MaxLength(20)]
        public string Source { get; set; } = "Manual";

        [MaxLength(200)]
        public string? Reason { get; set; }
    }

    /// <summary>Response after bulk-upserting daily inventory.</summary>
    public class ListingDailyInventoryBulkUpsertResponseDto
    {
        public int ListingId { get; set; }
        public int RequestedCount { get; set; }
        public int InsertedCount { get; set; }
        public int UpdatedCount { get; set; }
        public List<ListingDailyInventoryCalendarCellDto> Cells { get; set; } = new();
    }
}
