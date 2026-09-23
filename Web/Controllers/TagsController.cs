using Application.Commands.Tag;
using Application.Common.Models;
using Application.Constants;
using Application.Queries.Tag;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize]
[ApiController]
[Route("api/tags")]
public sealed class TagsController : ControllerBase
{
    private readonly ISender _sender;

    public TagsController(ISender sender)
    {
        _sender = sender;
    }

    [Authorize(Policy = Policies.ViewTagLibrary)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<string>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetTagLibraryQuery(), cancellationToken));
    }

    [Authorize(Policy = Policies.ViewTagLibrary)]
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<string>>> Search([FromQuery] GetTagBySearchQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [Authorize(Policy = Policies.ManageTagLibrary)]
    [HttpPost]
    public async Task<ActionResult<Result>> Create(AddNewTagCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
