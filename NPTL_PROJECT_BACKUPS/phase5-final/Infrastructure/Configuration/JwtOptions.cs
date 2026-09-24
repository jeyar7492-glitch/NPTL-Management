namespace NPTELManagement.Infrastructure.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "NPTELManagementApi";
    public string Audience { get; set; } = "NPTELManagementClient";
    public int ExpiryMinutes { get; set; } = 480; // 8 hours
}
