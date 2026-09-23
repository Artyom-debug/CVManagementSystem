using Application.Commands.Position;
using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Queries.Position;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize]
[ApiController]
[Route("api/positions")]
public sealed class PositionsController : ControllerBase
{
    private readonly ISender _sender;

    public PositionsController(ISender sender)
    {
        _sender = sender;
    }

    [AllowAnonymous]
    [HttpGet("public")]
    public async Task<ActionResult<PositionsPageDto>> GetPublic([FromQuery] GetPublicPositionsQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [HttpGet("available")]
    public async Task<ActionResult<PositionsPageDto>> GetAvailable([FromQuery] GetAvailablePositionsQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [Authorize(Policy = Policies.ManagePositions)]
    [HttpGet("managed")]
    public async Task<ActionResult<PositionsPageDto>> GetManaged([FromQuery] GetRecruiterPositionsQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [HttpGet("{positionId:guid}")]
    public async Task<ActionResult<DetailedPositionDto>> GetById(Guid positionId, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetDetailedPositionQuery(positionId), cancellationToken));
    }

    [Authorize(Policy = Policies.ManagePositions)]
    [HttpPost]
    public Task<ActionResult<Result>> Create(CreatePositionCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManagePositions)]
    [HttpGet("{positionId:guid}/cvs")]
    public async Task<ActionResult<PageResult<PositionCVDto>>> GetCVs(Guid positionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default) =>
        Ok(await _sender.Send(new GetPositionCVsQuery(positionId, page, pageSize), cancellationToken));

    [Authorize(Policy = Policies.ManagePositions)]
    [HttpPost("duplicate")]
    public Task<ActionResult<Result>> Duplicate(DuplicatePositionCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManagePositions)]
    [HttpPut]
    public Task<ActionResult<Result>> Update(UpdatePositionCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManagePositions)]
    [HttpPost("remove-range")]
    public Task<ActionResult<Result>> RemoveRange(RemovePositionRangeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ParticipateInDiscussions)]
    [HttpPost("discussion")]
    public Task<ActionResult<Result>> AddDiscussionPost(AddDiscussionPostCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    private async Task<ActionResult<Result>> SendCommand(IRequest<Result> command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
