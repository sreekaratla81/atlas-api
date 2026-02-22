using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class GuestCreateDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = null!;

    public string? IdProofUrl { get; set; }
}

public class GuestUpdateDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = null!;

    public string? IdProofUrl { get; set; }
}
