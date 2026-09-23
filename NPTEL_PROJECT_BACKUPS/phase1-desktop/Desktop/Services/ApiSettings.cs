namespace NPTELManagement.Desktop.Services;

/// <summary>
/// Centralized API configuration for the desktop client.
/// Provides a single source of truth for the backend base URL and endpoints.
/// </summary>
public class ApiSettings
{
    private static ApiSettings? _instance;
    public static ApiSettings Instance => _instance ??= new ApiSettings();

    public string BaseUrl { get; set; } = "http://127.0.0.1:5000";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    // Endpoint constants
    public const string HealthEndpoint = "/api/v1/health";
    public const string StudentLoginEndpoint = "/api/v1/auth/student/login";
    public const string StaffLoginEndpoint = "/api/v1/auth/staff/login";
    public const string AdminLoginEndpoint = "/api/v1/auth/admin/login";
    public const string LogoutEndpoint = "/api/v1/auth/logout";
    public const string StudentMeEndpoint = "/api/v1/student/me";
    public const string StaffMeEndpoint = "/api/v1/staff/me";
    public const string StaffStudentsEndpoint = "/api/v1/staff/students";
    public const string AdminMeEndpoint = "/api/v1/admin/me";

    public ApiSettings()
    {
        var envUrl = Environment.GetEnvironmentVariable("NPTEL_API_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            BaseUrl = envUrl.TrimEnd('/');
        }
    }
}
