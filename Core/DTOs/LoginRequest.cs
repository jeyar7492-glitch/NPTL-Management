using NPTELManagement.Core.Enums;

namespace NPTELManagement.Core.DTOs;

public class LoginRequest
{
    public UserRole Role { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
