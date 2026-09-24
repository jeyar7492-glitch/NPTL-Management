using System;
using System.IO;
using System.Linq;

namespace NPTELManagement.Desktop.Services;

/// <summary>
/// Centralized API configuration for the desktop client.
/// Provides hierarchical resolution of the backend base URL:
/// 1. Command-line argument: --api-url &lt;url&gt;
/// 2. NPTEL_API_URL environment variable
/// 3. Development/testing fallback (http://127.0.0.1:5000) when running test harnesses or in development
/// 4. appsettings.json beside executable
/// 5. Compiled production HTTPS default (https://api.nptel.jpcollege.ac.in)
/// </summary>
public class ApiSettings
{
    private static ApiSettings? _instance;
    public static ApiSettings Instance => _instance ??= new ApiSettings();

    public static void Initialize(string[]? args = null)
    {
        _instance = new ApiSettings(args);
    }

    public const string ProductionDefaultUrl = "https://api.nptel.jpcollege.ac.in";
    public const string LocalDevDefaultUrl = "http://127.0.0.1:5000";

    public string BaseUrl { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    // Endpoint constants
    public const string HealthEndpoint = "/api/v1/health";
    public const string StudentLoginEndpoint = "/api/v1/auth/student/login";
    public const string StaffLoginEndpoint = "/api/v1/auth/staff/login";
    public const string AdminLoginEndpoint = "/api/v1/auth/admin/login";
    public const string LogoutEndpoint = "/api/v1/auth/logout";
    public const string StudentMeEndpoint = "/api/v1/student/me";
    public const string StudentDashboardSummaryEndpoint = "/api/v1/student/dashboard-summary";
    public const string StudentCoursesEndpoint = "/api/v1/student/courses";
    public const string StudentNotificationsEndpoint = "/api/v1/student/notifications";
    public const string StaffMeEndpoint = "/api/v1/staff/me";
    public const string StaffDashboardSummaryEndpoint = "/api/v1/staff/dashboard-summary";
    public const string StaffStudentsEndpoint = "/api/v1/staff/students";
    public const string StaffReportsPreviewEndpoint = "/api/v1/staff/reports/preview";
    public const string AdminMeEndpoint = "/api/v1/admin/me";

    public ApiSettings(string[]? args = null)
    {
        // 1. Command line argument: --api-url <url>
        var cmdUrl = ValidateAndNormalizeUrl(TryReadUrlFromCommandLine(args));
        if (!string.IsNullOrEmpty(cmdUrl))
        {
            BaseUrl = cmdUrl;
            return;
        }

        // 2. NPTEL_API_URL environment variable
        var envUrl = ValidateAndNormalizeUrl(Environment.GetEnvironmentVariable("NPTEL_API_URL"));
        if (!string.IsNullOrEmpty(envUrl))
        {
            BaseUrl = envUrl;
            return;
        }

        // 3. Localhost fallback PRESERVED ONLY for Development/testing
        if (IsDevelopmentOrTestEnvironment(args))
        {
            BaseUrl = LocalDevDefaultUrl;
            return;
        }

        // 4. Desktop/appsettings.json beside executable
        var configUrl = ValidateAndNormalizeUrl(TryReadUrlFromConfigFile());
        if (!string.IsNullOrEmpty(configUrl))
        {
            BaseUrl = configUrl;
            return;
        }

        // 5. Compiled production HTTPS default
        BaseUrl = ProductionDefaultUrl;
    }

    public static string? ValidateAndNormalizeUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return null;
        var trimmed = rawUrl.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // Must be http or https
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        // Reject credentials in user info (e.g., http://user:pass@host)
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        return trimmed.TrimEnd('/');
    }

    private static string? TryReadUrlFromCommandLine(string[]? args)
    {
        try
        {
            var cmdArgs = args ?? Environment.GetCommandLineArgs();
            for (int i = 0; i < cmdArgs.Length - 1; i++)
            {
                if (string.Equals(cmdArgs[i], "--api-url", StringComparison.OrdinalIgnoreCase))
                {
                    return cmdArgs[i + 1];
                }
            }
        }
        catch { }
        return null;
    }

    private static string? TryReadUrlFromConfigFile()
    {
        try
        {
            var pathsToCheck = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Desktop", "appsettings.json")
            };

            foreach (var configPath in pathsToCheck)
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("ApiSettings", out var apiSection) &&
                        apiSection.TryGetProperty("BaseUrl", out var baseUrlElement))
                    {
                        var url = baseUrlElement.GetString();
                        if (!string.IsNullOrWhiteSpace(url)) return url;
                    }
                    if (root.TryGetProperty("BaseUrl", out var rootUrlElement))
                    {
                        var url = rootUrlElement.GetString();
                        if (!string.IsNullOrWhiteSpace(url)) return url;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static bool IsDevelopmentOrTestEnvironment(string[]? args)
    {
        try
        {
            var cmdArgs = args ?? Environment.GetCommandLineArgs();
            if (cmdArgs.Any(a => string.Equals(a, "--run-e2e-tests", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("NPTEL_ENVIRONMENT");

            return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(env, "Testing", StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        return false;
    }
}
