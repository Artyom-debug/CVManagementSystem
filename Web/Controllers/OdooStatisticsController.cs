using Application.Dtos;
using Application.Commands.Integrations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/odoo/position-statistics")]
public sealed class OdooStatisticsController : ControllerBase
{
    private readonly ISender _sender;

    public OdooStatisticsController(ISender sender) => _sender = sender;

    [AllowAnonymous]
    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<OdooPositionStatisticsDto>> Get(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized();

        return Ok(await _sender.Send(new RedeemOdooPositionStatisticsCommand(token), cancellationToken));
    }
}
