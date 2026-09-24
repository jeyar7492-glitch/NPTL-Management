using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NPTELManagement.Core.Interfaces;
using NPTELManagement.Infrastructure.Authentication;
using NPTELManagement.Infrastructure.Data;
using NPTELManagement.Infrastructure.Repositories;

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
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                                   ?? configuration["SUPABASE_DB_CONNECTION"];

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
        services.AddHostedService<Services.NotificationRuleEngineBackgroundService>();

        return services;
    }
}
