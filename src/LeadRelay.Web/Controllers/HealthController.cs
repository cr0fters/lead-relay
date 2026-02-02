using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { ok = true });
}
