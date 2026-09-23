using System.ComponentModel.DataAnnotations;

namespace NPTELManagement.Core.DTOs;

public class StudentLoginDto
{
    [Required(ErrorMessage = "Register number is required.")]
    public string RegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class StaffLoginDto
{
    [Required(ErrorMessage = "Staff ID is required.")]
    public string StaffId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class AdminLoginDto
{
    [Required(ErrorMessage = "Admin ID is required.")]
    public string AdminId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class AuthSuccessResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
