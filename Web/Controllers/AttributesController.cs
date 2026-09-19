using Application.Commands.Attribute;
using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Queries.Attribute;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize]
[ApiController]
[Route("api/attributes")]
public sealed class AttributesController : ControllerBase
{
    private readonly ISender _sender;

    public AttributesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<PageResult<AttributeDto>>> Get([FromQuery] GetAttributesQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<AttributeDto>>> Search([FromQuery] GetAttributeBySearchQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [HttpGet("recently-used")]
    public async Task<ActionResult<IReadOnlyList<AttributeDto>>> GetRecentlyUsed([FromQuery] GetRecentlyUsedAttributesQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [HttpGet("{attributeId:guid}")]
    public async Task<ActionResult<DetailedAttributeDto>> GetById(Guid attributeId, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetDetailedAttributeQuery(attributeId), cancellationToken));
    }

    [Authorize(Policy = Policies.ManageAttributeLibrary)]
    [HttpPost]
    public Task<ActionResult<Result>> Create(CreateAttributeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageSystemAttributes)]
    [HttpPost("system")]
    public Task<ActionResult<Result>> CreateSystem(CreateSystemAttributeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageAttributeLibrary)]
    [HttpPut]
    public Task<ActionResult<Result>> Update(UpdateAttributeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageAttributeLibrary)]
    [HttpDelete]
    public Task<ActionResult<Result>> Delete(DeleteAttributeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);


    [Authorize(Policy = Policies.ManageAttributeLibrary)]
    [HttpPost("delete-range")]
    public Task<ActionResult<Result>> DeleteRange(DeleteAttributeRangeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    private async Task<ActionResult<Result>> SendCommand(IRequest<Result> command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
