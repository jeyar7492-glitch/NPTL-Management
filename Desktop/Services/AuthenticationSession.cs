using NPTELManagement.Core.DTOs;

namespace NPTELManagement.Desktop.Services;

/// <summary>
/// In-memory implementation of IAuthenticationSession.
/// Maintains user session state strictly in RAM.
/// </summary>
public class AuthenticationSession : IAuthenticationSession
{
    private static AuthenticationSession? _instance;
    public static AuthenticationSession Instance => _instance ??= new AuthenticationSession();

    private string? _accessToken;
    private string? _userRole;
    private string? _identifier;
    private string? _userName;
    private DateTime? _expiresAt;

    public string? AccessToken => _accessToken;
    public string? UserRole => _userRole;
    public string? Identifier => _identifier;
    public string? UserName => _userName;
    public DateTime? ExpiresAt => _expiresAt;
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    public event Action? SessionChanged;
    public event Action? SessionExpired;

    public void SetSession(AuthSuccessResponse authResponse)
    {
        _accessToken = authResponse.AccessToken;
        _userRole = authResponse.Role;
        _identifier = authResponse.Identifier;
        _userName = authResponse.Name;
        _expiresAt = authResponse.ExpiresAt;

        SessionChanged?.Invoke();
    }

    public void ClearSession()
    {
        _accessToken = null;
        _userRole = null;
        _identifier = null;
        _userName = null;
        _expiresAt = null;

        SessionChanged?.Invoke();
        SessionExpired?.Invoke();
    }
}
