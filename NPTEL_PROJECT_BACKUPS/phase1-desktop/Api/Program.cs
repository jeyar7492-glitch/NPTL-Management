using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NPTELManagement.Api.Middleware;
using NPTELManagement.Infrastructure.Configuration;

// CLI Utility: Secure Password Hashing
if (args.Length > 0 && args[0] == "--hash-password")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Error: Please provide a password to hash.");
        return;
    }
    var hasher = new NPTELManagement.Infrastructure.Authentication.PasswordHasher();
    Console.WriteLine(hasher.HashPassword(args[1]));
    return;
}

// CLI Utility: Generate Secure Seed Script with real BCrypt Hashes
if (args.Length > 0 && args[0] == "--generate-seed-sql")
{
    var hasher = new NPTELManagement.Infrastructure.Authentication.PasswordHasher();
    var adminPwd = Environment.GetEnvironmentVariable("DEV_ADMIN_PASSWORD") ?? "Admin@Nptel2026";
    var staffPwd = Environment.GetEnvironmentVariable("DEV_STAFF_PASSWORD") ?? "Staff@Nptel2026";
    var studentPwd = Environment.GetEnvironmentVariable("DEV_STUDENT_PASSWORD") ?? "Student@Nptel2026";

    var adminHash = hasher.HashPassword(adminPwd);
    var staffHash = hasher.HashPassword(staffPwd);
    var studentHash = hasher.HashPassword(studentPwd);

    var seedTemplatePath = Path.Combine(Directory.GetCurrentDirectory(), "Database", "02_seed_initial_accounts.sql");
    if (!File.Exists(seedTemplatePath))
    {
        seedTemplatePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Database", "02_seed_initial_accounts.sql");
    }
    if (!File.Exists(seedTemplatePath))
    {
        seedTemplatePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Database", "02_seed_initial_accounts.sql");
    }

    if (File.Exists(seedTemplatePath))
    {
        var sql = File.ReadAllText(seedTemplatePath)
            .Replace("{{ADMIN_PASSWORD_HASH}}", adminHash)
            .Replace("{{STAFF_PASSWORD_HASH}}", staffHash)
            .Replace("{{STUDENT_PASSWORD_HASH}}", studentHash);

        var outputPath = Path.Combine(Path.GetDirectoryName(seedTemplatePath)!, "02_seed_initial_accounts.ready.sql");
        File.WriteAllText(outputPath, sql);
        Console.WriteLine($"SUCCESS: Generated seed script at {outputPath}");
    }
    else
    {
        Console.WriteLine($"ADMIN_HASH={adminHash}");
        Console.WriteLine($"STAFF_HASH={staffHash}");
        Console.WriteLine($"STUDENT_HASH={studentHash}");
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers
builder.Services.AddControllers();

// 2. Add Infrastructure Services (DbContext, Repositories, PasswordHasher, JwtTokenService, RateLimiter)
builder.Services.AddInfrastructure(builder.Configuration);

// 3. JWT Authentication Configuration
var jwtSecret = builder.Configuration["JWT_SECRET"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = builder.Configuration["Jwt:Secret"];
}
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = "Default_Dev_Secret_Key_At_Least_32_Characters_Long_For_Local_Dev!";
}

var jwtIssuer = builder.Configuration["JWT_ISSUER"];
if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    jwtIssuer = builder.Configuration["Jwt:Issuer"];
}
if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    jwtIssuer = "NPTELManagementApi";
}

var jwtAudience = builder.Configuration["JWT_AUDIENCE"];
if (string.IsNullOrWhiteSpace(jwtAudience))
{
    jwtAudience = builder.Configuration["Jwt:Audience"];
}
if (string.IsNullOrWhiteSpace(jwtAudience))
{
    jwtAudience = "NPTELManagementClient";
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Enabled for dev flex, HTTPS enforced in prod
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 4. Role & Policy Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOnly", policy => policy.RequireRole("Student"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("Staff"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// 5. Restrictive CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DesktopClientPolicy", policy =>
    {
        policy.WithOrigins("http://localhost", "https://localhost", "app://nptel-desktop")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 6. Swagger Documentation with JWT Bearer Definition
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NPTEL Management System API",
        Version = "v1",
        Description = "Production REST API for NPTEL Registration, Exam Tracking, and Academic Management."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT Bearer token: Bearer {your token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed initial development/test records if in-memory database or explicit --seed-db flag
if (app.Configuration.GetValue<bool>("USE_INMEMORY_DB") || args.Contains("--seed-db"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NPTELManagement.Infrastructure.Data.ApplicationDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<NPTELManagement.Core.Interfaces.IPasswordHasher>();
    await NPTELManagement.Infrastructure.Data.DatabaseSeeder.SeedAsync(dbContext, hasher);
}

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger UI in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NPTEL Management API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("DesktopClientPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Required for integration testing WebApplicationFactory if used
public partial class Program { }
