using Application.Commands.CV;
using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Queries.CV;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize]
[ApiController]
[Route("api/cvs")]
public sealed class CVsController : ControllerBase
{
    private readonly ISender _sender;

    public CVsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{cvId:guid}")]
    public async Task<ActionResult<CVDetailsDto>> GetById(Guid cvId, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetCVQuery(cvId), cancellationToken));
    }

    [Authorize(Policy = Policies.ManageCV)]
    [HttpPost]
    public Task<ActionResult<Result>> Create(CreateNewCVCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageCV)]
    [HttpPut("attributes")]
    public Task<ActionResult<Result>> SaveAttributeValues(SaveCVAttributeValuesCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageCV)]
    [HttpPost("publish")]
    public Task<ActionResult<Result>> Publish(PublishCVCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageCV)]
    [HttpDelete]
    public Task<ActionResult<Result>> Remove(RemoveCVCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.LikeCV)]
    [HttpPost("likes")]
    public Task<ActionResult<Result>> AddLike(AddCVLikeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.LikeCV)]
    [HttpDelete("likes")]
    public Task<ActionResult<Result>> RemoveLike(RemoveCVLikeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    private async Task<ActionResult<Result>> SendCommand(IRequest<Result> command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
