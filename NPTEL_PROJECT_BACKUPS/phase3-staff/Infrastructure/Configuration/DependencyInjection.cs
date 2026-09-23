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
                options.Secret = "Default_Dev_Secret_Key_At_Least_32_Characters_Long_For_Local_Dev!";
            }
        });

        // Configure Database DbContext
        var useInMemory = configuration.GetValue<bool>("USE_INMEMORY_DB");
        if (useInMemory)
        {
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
                // Configured for Supabase PostgreSQL default
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

        return services;
    }
}
