using System.Text.Json.Serialization;

namespace NPTELManagement.Desktop.Models;

public class HealthCheckResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("database")]
    public string Database { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    public bool IsHealthy => 
        string.Equals(Status, "ok", StringComparison.OrdinalIgnoreCase) && 
        string.Equals(Database, "connected", StringComparison.OrdinalIgnoreCase);
}
