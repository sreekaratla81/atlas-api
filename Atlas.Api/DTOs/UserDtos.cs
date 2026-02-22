using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

/// <summary>User data returned by the API. Excludes PasswordHash for security.</summary>
public class UserResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Role { get; set; } = null!;
}

/// <summary>Request body for creating a user account.</summary>
public class UserCreateDto
{
    [Required]
    public string Name { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Phone { get; set; } = null!;

    [Required]
    public string Role { get; set; } = null!;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = null!;
}
