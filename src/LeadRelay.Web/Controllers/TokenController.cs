using LeadRelay.Application.Widget;
using LeadRelay.Contracts.Widget;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

[ApiController]
public sealed class TokenController(CreateWidgetTokenHandler handler) : ControllerBase
{
    [HttpPost("/v1/widget/token")]
    public async Task<IActionResult> CreateToken([FromBody] WidgetTokenRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        if (result is null) return Unauthorized();
        return Ok(result);
    }
}