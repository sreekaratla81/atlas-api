namespace Atlas.Api.Models
{
    public class Guest
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Phone { get; set; }
        public string? PhoneE164 { get; set; }
        public required string Email { get; set; }
        public string NameSearch { get; set; } = string.Empty;
        public string? IdProofUrl { get; set; }
    }
}
