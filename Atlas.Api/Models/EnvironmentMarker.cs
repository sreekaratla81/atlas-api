using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class EnvironmentMarker
    {
        public int Id { get; set; }

        [MaxLength(10)]
        public required string Marker { get; set; }
    }
}
