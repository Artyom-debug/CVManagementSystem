using Application.Commands.Integrations;
using Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize]
[ApiController]
[Route("api/support-tickets")]
public sealed class SupportTicketsController : ControllerBase
{
    private readonly ISender _sender;

    public SupportTicketsController(ISender sender) => _sender = sender;

    [HttpPost]
    public async Task<ActionResult<Result>> Create(
        [FromBody] CreateSupportTicketCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
