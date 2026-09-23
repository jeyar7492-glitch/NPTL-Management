using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Desktop.Services;

/// <summary>
/// Manages the in-memory authentication state of the desktop application.
/// Token and credentials are NEVER persisted to disk or logs.
/// </summary>
public interface IAuthenticationSession
{
    string? AccessToken { get; }
    string? UserRole { get; }
    string? Identifier { get; }
    string? UserName { get; }
    DateTime? ExpiresAt { get; }
    bool IsAuthenticated { get; }

    void SetSession(AuthSuccessResponse authResponse);
    void ClearSession();

    event Action? SessionChanged;
    event Action? SessionExpired;
}
