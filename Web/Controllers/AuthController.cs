using Application.Commands.Auth;
using Application.Common.Models;
using Infrastructure.Services.Identity;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Web.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;

    public AuthController(ISender sender, SignInManager<ApplicationUser> signInManager, IAuthenticationSchemeProvider authenticationSchemeProvider)
    {
        _sender = sender;
        _signInManager = signInManager;
        _authenticationSchemeProvider = authenticationSchemeProvider;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<Result>> Register(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    public async Task<ActionResult<Result>> ResendConfirmation(ResendConfirmationEmailCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResult>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : Unauthorized(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthenticationResult>> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Succeeded ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult<Result>> Logout(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new LogoutCommand(), cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<ActionResult<Result>> ConfirmEmail([FromQuery] ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        var browserRequest = Request.GetTypedHeaders().Accept?.Any(value =>
            string.Equals(value.MediaType.Value, "text/html", StringComparison.OrdinalIgnoreCase)) == true;
        var result = browserRequest && string.IsNullOrWhiteSpace(command.Token)
            ? Result.Failure("Email confirmation token is missing.")
            : await _sender.Send(command, cancellationToken);
        if (browserRequest)
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            return new ContentResult
            {
                Content = Web.Pages.EmailConfirmationPage.Render(result.Succeeded),
                ContentType = "text/html; charset=utf-8",
                StatusCode = result.Succeeded ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest
            };
        }
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [AllowAnonymous]
    [HttpGet("external/{provider}")]
    public async Task<IActionResult> ExternalLogin(string provider)
    {
        var authenticationScheme = GetAuthenticationScheme(provider);
        if (authenticationScheme is null)
            return BadRequest(new ProblemDetails { Title = "Unknown external authentication provider.", Status = StatusCodes.Status400BadRequest });

        if (await _authenticationSchemeProvider.GetSchemeAsync(authenticationScheme) is null)
            return BadRequest(new ProblemDetails { Title = $"External authentication provider '{authenticationScheme}' is not configured.", Status = StatusCodes.Status400BadRequest });

        var callbackUrl = Url.ActionLink(nameof(ExternalLoginCallback), values: null)
            ?? throw new InvalidOperationException("External login callback URL could not be created.");

        var properties = _signInManager.ConfigureExternalAuthenticationProperties(authenticationScheme, callbackUrl);
        return Challenge(properties, authenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet("external/callback")]
    public async Task<ActionResult<AuthenticationResult>> ExternalLoginCallback(CancellationToken cancellationToken)
    {
        var externalLogin = await _signInManager.GetExternalLoginInfoAsync();
        if (externalLogin is null)
            return Unauthorized(AuthenticationResult.Failure("External authentication failed."));

        var email = externalLogin.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return BadRequest(AuthenticationResult.Failure("The external provider did not return an email address."));
        }

        var result = await _sender.Send(new ExternalLoginCommand(externalLogin.LoginProvider, externalLogin.ProviderKey, email), cancellationToken);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (!result.Succeeded)
            return BadRequest(result);

        return Ok(result);
    }

    private static string? GetAuthenticationScheme(string provider)
    {
        if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
            return "Google";
        if (provider.Equals("facebook", StringComparison.OrdinalIgnoreCase))
            return "Facebook";

        return null;
    }
}
