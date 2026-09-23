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

    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        bool dbConnected = false;
        try
        {
            dbConnected = await _dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Database connectivity check failed: {Message}", ex.Message);
            dbConnected = false;
        }

        var result = new
        {
            status = dbConnected ? "ok" : "degraded",
            database = dbConnected ? "connected" : "disconnected",
            timestamp = DateTime.UtcNow
        };

        return Ok(result);
    }
}
