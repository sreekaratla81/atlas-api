namespace Atlas.Api.Services
{
    /// <summary>
    /// Configuration settings for MSG91 SMS service
    /// </summary>
    public class Msg91Settings
    {
        /// <summary>
        /// MSG91 AuthKey (API key)
        /// </summary>
        public string AuthKey { get; set; } = string.Empty;

        /// <summary>
        /// MSG91 Sender ID (6 characters max)
        /// </summary>
        public string SenderId { get; set; } = string.Empty;

        /// <summary>
        /// MSG91 Route (1 for promotional, 4 for transactional)
        /// </summary>
        public string Route { get; set; } = "4";

        /// <summary>
        /// MSG91 Template ID for Flow API (DLT registered template)
        /// </summary>
        public string TemplateId { get; set; } = string.Empty;
    }
}
