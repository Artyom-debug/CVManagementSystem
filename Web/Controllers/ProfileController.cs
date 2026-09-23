using MediatR;
using Application.Commands.Profile;
using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Queries.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize]
[ApiController]
[Route("api/profiles")]
public sealed class ProfileController : ControllerBase
{
    private readonly ISender _sender;

    public ProfileController(ISender sender)
    {
        _sender = sender;
    }

    [Authorize(Policy = Policies.ManagePersonalProfile)]
    [HttpGet("me")]
    public async Task<ActionResult<ProfileDto>> GetPersonal(CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetPersonalProfileQuery(), cancellationToken));
    }

    [Authorize(Policy = Policies.ViewFullCandidateProfile)]
    [HttpGet("{profileId:guid}")]
    public async Task<ActionResult<ReadonlyProfileDto>> GetReadonly(Guid profileId, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetReadonlyProfileQuery(profileId), cancellationToken));
    }

    [Authorize(Policy = Policies.ManagePersonalProfile)]
    [HttpPut("initial")]
    public Task<ActionResult<Result>> CompleteInitial(CompleteInitialProfileCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManagePersonalProfile)]
    [HttpPost("{profileId:guid}/attributes/{attributeId:guid}/image-upload-data")]
    public async Task<ActionResult<ImageUploadData>> CreateImageUploadData(Guid profileId, Guid attributeId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateImageUploadDataCommand(profileId, attributeId), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.ManageCandidateProfile)]
    [HttpPost("attributes")]
    public Task<ActionResult<Result>> AddAttribute(AddNewProfileAttributeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManagePersonalProfile)]
    [HttpPut("attributes")]
    public Task<ActionResult<Result>> UpdateAttribute(UpdateProfileAttributeValueCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageCandidateProfile)]
    [HttpDelete("attributes")]
    public Task<ActionResult<Result>> RemoveAttribute(RemoveProfileAttributeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageCandidateProfile)]
    [HttpPost("projects")]
    public Task<ActionResult<Result>> AddProject(AddNewProjectCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageCandidateProfile)]
    [HttpPut("projects")]
    public Task<ActionResult<Result>> UpdateProject(UpdateProjectInfoCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    [Authorize(Policy = Policies.ManageCandidateProfile)]
    [HttpPost("projects/delete-range")]
    public Task<ActionResult<Result>> DeleteProjectsRange(DeleteProjectsRangeCommand command, CancellationToken cancellationToken) =>
        SendCommand(command, cancellationToken);

    private async Task<ActionResult<Result>> SendCommand(IRequest<Result> command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}

