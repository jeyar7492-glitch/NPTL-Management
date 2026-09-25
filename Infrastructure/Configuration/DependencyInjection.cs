using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Authentication;
using NPTELManagement.Infrastructure.Data;
using NPTELManagement.Infrastructure.Repositories;
using Npgsql;

namespace NPTELManagement.Infrastructure.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var isProduction = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);

        // Bind JwtOptions with environment fallback
        services.Configure<JwtOptions>(options =>
        {
            configuration.GetSection(JwtOptions.SectionName).Bind(options);
            var envSecret = configuration["JWT_SECRET"];
            if (!string.IsNullOrWhiteSpace(envSecret))
            {
                options.Secret = envSecret;
            }
            if (string.IsNullOrWhiteSpace(options.Secret))
            {
                if (isProduction)
                {
                    throw new InvalidOperationException(
                        "JWT_SECRET is mandatory in Production. Please configure a secure secret key (minimum 32 characters) via environment variable 'JWT_SECRET' or configuration.");
                }
                options.Secret = "Default_Dev_Secret_Key_At_Least_32_Characters_Long_For_Local_Dev!";
            }
            else if (isProduction && options.Secret.Length < 32)
            {
                throw new InvalidOperationException(
                    $"JWT_SECRET in Production must be at least 32 characters long. Current length: {options.Secret.Length}.");
            }
            else if (isProduction && options.Secret == "Default_Dev_Secret_Key_At_Least_32_Characters_Long_For_Local_Dev!")
            {
                throw new InvalidOperationException(
                    "The development fallback JWT secret cannot be used in Production. Please configure a unique, high-entropy JWT_SECRET.");
            }
        });

        // Configure Database DbContext
        var useInMemory = configuration.GetValue<bool>("USE_INMEMORY_DB");
        if (useInMemory)
        {
            if (isProduction)
            {
                throw new InvalidOperationException(
                    "USE_INMEMORY_DB cannot be enabled in Production. A valid Supabase PostgreSQL database connection is required.");
            }
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("NptelDb");
            });
        }
        else
        {
            var rawConnectionString = configuration["SUPABASE_DB_CONNECTION"];
            if (string.IsNullOrWhiteSpace(rawConnectionString))
            {
                rawConnectionString = configuration.GetConnectionString("DefaultConnection");
            }

            var connectionString = NormalizeDatabaseConnectionString(rawConnectionString);

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    });
                });
            }
            else
            {
                if (isProduction)
                {
                    throw new InvalidOperationException(
                        "Database connection string ('SUPABASE_DB_CONNECTION' or 'ConnectionStrings:DefaultConnection') is mandatory in Production. Localhost fallback is disabled.");
                }

                // Configured for local development only
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseNpgsql("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres");
                });
            }
        }

        // Core security services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IStaffAuthorizationService, StaffAuthorizationService>();
        services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IRegistrationRepository, RegistrationRepository>();
        services.AddScoped<ICertificateRepository, CertificateRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        // Student Workflow Services
        services.AddScoped<IStudentCourseService, Services.StudentCourseService>();
        services.AddScoped<IStudentNotificationService, Services.StudentNotificationService>();
        // Staff Services
        services.AddScoped<IStaffStudentService, Services.StaffStudentService>();
        services.AddScoped<IStaffReportService, Services.StaffReportService>();

        // Phase 4 - Real Private Cloud Object Storage & Audit Logging
        services.AddHttpClient<IPrivateCloudStorageService, Storage.SupabasePrivateStorageService>();
        services.AddScoped<IAuditLogService, Services.AuditLogService>();

        // Phase 4 - Admin Management Services
        services.AddScoped<IAdminStudentService, Services.AdminStudentService>();
        services.AddScoped<IAdminStaffService, Services.AdminStaffService>();
        services.AddScoped<IAdminCourseService, Services.AdminCourseService>();
        services.AddScoped<IAdminRegistrationService, Services.AdminRegistrationService>();
        services.AddScoped<IAdminExamService, Services.AdminExamService>();
        services.AddScoped<IAdminCertificateService, Services.AdminCertificateService>();
        services.AddScoped<IAdminNotificationService, Services.AdminNotificationService>();
        services.AddScoped<IAdminReportService, Services.AdminReportService>();

        // Phase 4 - Background Automation Rules Worker
        // In serverless/stateless container environments (e.g. Vercel), workers can be disabled via ENABLE_BACKGROUND_WORKER=false.
        // Defaults to true for local development and traditional persistent server hosts (e.g. Render).
        var enableWorkerConfig = configuration["ENABLE_BACKGROUND_WORKER"];
        var enableBackgroundWorker = true;
        if (!string.IsNullOrWhiteSpace(enableWorkerConfig))
        {
            if (bool.TryParse(enableWorkerConfig, out var parsed))
            {
                enableBackgroundWorker = parsed;
            }
            else if (enableWorkerConfig.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                     enableWorkerConfig.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                enableBackgroundWorker = false;
            }
        }

        if (enableBackgroundWorker)
        {
            services.AddHostedService<Services.NotificationRuleEngineBackgroundService>();
        }

        return services;
    }

#pragma warning disable CS0618 // Obsolete Npgsql TrustServerCertificate property retained for backwards compatibility
    /// <summary>
    /// Normalizes a PostgreSQL database connection string or URI into a standard Npgsql key/value connection string.
    /// Supports:
    /// 1. Standard Npgsql key/value pairs (e.g. Host=...;Port=5432;Database=postgres;...)
    /// 2. Supabase / PostgreSQL URI format (e.g. postgresql://user:pass@host:5432/dbname or postgres://...)
    /// 3. Safely URL-decodes username and password.
    /// 4. Preserves Host, Port, Database, Username, Password, SslMode=Require, TrustServerCertificate=true.
    /// 5. Never logs or leaks passwords or credentials in exceptions.
    /// </summary>
    public static string? NormalizeDatabaseConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
                {
                    throw new InvalidOperationException("Invalid PostgreSQL database connection configuration.");
                }

                var userInfo = uri.UserInfo;
                string username = userInfo;
                string password = string.Empty;

                var colonIdx = userInfo.IndexOf(':');
                if (colonIdx >= 0)
                {
                    username = userInfo.Substring(0, colonIdx);
                    password = userInfo.Substring(colonIdx + 1);
                }

                username = Uri.UnescapeDataString(username);
                password = Uri.UnescapeDataString(password);

                var dbName = uri.AbsolutePath.TrimStart('/');
                if (string.IsNullOrWhiteSpace(dbName))
                {
                    dbName = "postgres";
                }

                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.Port > 0 ? uri.Port : 5432,
                    Database = dbName,
                    Username = username,
                    Password = password,
                    SslMode = SslMode.Require,
                    TrustServerCertificate = true
                };

                if (!string.IsNullOrWhiteSpace(uri.Query))
                {
                    var query = uri.Query.TrimStart('?');
                    var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split('=', 2);
                        var key = Uri.UnescapeDataString(kv[0]).ToLowerInvariant();
                        var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;

                        if (key == "sslmode" && Enum.TryParse<SslMode>(val, true, out var parsedSslMode))
                        {
                            builder.SslMode = parsedSslMode;
                        }
                        else if (key == "trustservercertificate" || key == "trust_server_certificate")
                        {
                            if (bool.TryParse(val, out var trust))
                            {
                                builder.TrustServerCertificate = trust;
                            }
                        }
                    }
                }

                return builder.ConnectionString;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException("Invalid PostgreSQL database connection configuration.");
            }
        }
        else
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(trimmed);
                if (!builder.ContainsKey("SSL Mode") && !builder.ContainsKey("SslMode"))
                {
                    builder.SslMode = SslMode.Require;
                }
                if (!builder.ContainsKey("Trust Server Certificate") && !builder.ContainsKey("TrustServerCertificate"))
                {
                    builder.TrustServerCertificate = true;
                }
                return builder.ConnectionString;
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Invalid PostgreSQL database connection configuration.");
            }
        }
    }
#pragma warning restore CS0618
}
