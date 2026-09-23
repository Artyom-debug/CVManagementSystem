using Application.Commands.Auth;
using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Queries.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize(Policy = Policies.ManageUsers)]
[ApiController]
[Route("api/admin/users")]
public sealed class AdminController : ControllerBase
{
    private readonly ISender _sender;

    public AdminController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<PageResult<IdentityUserDto>>> GetUsers([FromQuery] GetUsersQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [HttpPut("{userId}/block")]
    public Task<ActionResult<Result>> Block(string userId, CancellationToken cancellationToken) =>
        SendCommand(new BlockUserCommand(userId), cancellationToken);

    [HttpPut("{userId}/unblock")]
    public Task<ActionResult<Result>> Unblock(string userId, CancellationToken cancellationToken) =>
        SendCommand(new UnblockUserCommand(userId), cancellationToken);

    [HttpDelete("{userId}")]
    public Task<ActionResult<Result>> Delete(string userId, CancellationToken cancellationToken) =>
        SendCommand(new DeleteUserCommand(userId), cancellationToken);

    [HttpPost("{userId}/roles/{role}")]
    public Task<ActionResult<Result>> AddRole(string userId, string role, CancellationToken cancellationToken) =>
        SendCommand(new AddUserToRoleCommand(userId, role), cancellationToken);

    [HttpDelete("{userId}/roles/{role}")]
    public Task<ActionResult<Result>> RemoveRole(string userId, string role, CancellationToken cancellationToken) =>
        SendCommand(new RemoveUserFromRoleCommand(userId, role), cancellationToken);

    private async Task<ActionResult<Result>> SendCommand(IRequest<Result> command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
