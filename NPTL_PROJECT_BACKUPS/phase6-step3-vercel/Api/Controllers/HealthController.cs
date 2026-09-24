using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPTELManagement.Infrastructure.Data;

namespace NPTELManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ApplicationDbContext dbContext, ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Combined Health / Readiness Endpoint.
    /// Returns 200 OK when database is connected; 503 Service Unavailable when database is degraded.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var dbConnected = await CheckDatabaseConnectionAsync(cancellationToken);

        var result = new
        {
            status = dbConnected ? "ok" : "degraded",
            database = dbConnected ? "connected" : "disconnected",
            timestamp = DateTime.UtcNow
        };

        if (!dbConnected)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Liveness Probe for container orchestrators (e.g. Kubernetes, AWS ECS, Docker).
    /// Returns 200 OK as long as the web process is running.
    /// </summary>
    [HttpGet("live")]
    public IActionResult GetLiveness()
    {
        return Ok(new
        {
            status = "alive",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Readiness Probe for load balancers and orchestrators.
    /// Returns 200 OK when dependencies are ready; 503 Service Unavailable when database is disconnected.
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness(CancellationToken cancellationToken)
    {
        var dbConnected = await CheckDatabaseConnectionAsync(cancellationToken);

        var result = new
        {
            status = dbConnected ? "ready" : "not_ready",
            database = dbConnected ? "connected" : "disconnected",
            timestamp = DateTime.UtcNow
        };

        if (!dbConnected)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        return Ok(result);
    }

    private async Task<bool> CheckDatabaseConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log generic error message without exposing connection strings or credentials
            _logger.LogWarning("Database connectivity probe failed: {ErrorMessage}", ex.Message);
            return false;
        }
    }
}
