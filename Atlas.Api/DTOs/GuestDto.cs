namespace Atlas.Api.DTOs
{
    /// <summary>Guest data returned by the API.</summary>
    public class GuestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? IdProofUrl { get; set; }
    }
}
