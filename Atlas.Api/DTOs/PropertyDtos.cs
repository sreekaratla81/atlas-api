using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class PropertyCreateDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    public string Address { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string OwnerName { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    public string ContactPhone { get; set; } = null!;

    public decimal? CommissionPercent { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Active";
}

public class PropertyResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string OwnerName { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;
    public decimal? CommissionPercent { get; set; }
    public string Status { get; set; } = null!;
}
