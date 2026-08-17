using Microsoft.AspNetCore.Mvc;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeadRelay.Web.Controllers;

[ApiController]
public sealed class HealthController(LeadRelayDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet("/health/live")]
    public IActionResult Live() => Ok(new { ok = true });

    [HttpGet("/health")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            var databaseAvailable = await db.Database.CanConnectAsync(ct);
            return databaseAvailable
                ? Ok(new { ok = true, database = "healthy" })
                : StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, database = "unavailable" });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "LeadRelay readiness health check failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, database = "unavailable" });
        }
    }
}
