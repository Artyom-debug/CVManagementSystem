using System.Reflection;
using Application.Commands.Auth;
using Application.Common.Models;
using Application.Interfaces;
using Infrastructure.Services.Identity;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

var users = new Users();
var signIn = new SignIn(users);
var redis = DispatchProxy.Create<IConnectionMultiplexer, RedisProxy>();
var service = new IdentityService(users, signIn, null!, null!, null!, redis);
var login = await service.VerifyUserPasswordAsync("Password123!", "blocked@example.test", default);
if (login.Result.Succeeded || !login.Result.Errors.Single().Contains("blocked") || login.UserId != "") throw new Exception("Blocked login must return a block message and no user ID.");
var registration = await service.StartRegistrationAsync("Password123!", " BLOCKED@example.test ", default);
if (registration.Result.Succeeded || registration.ConfirmationToken != "" || !registration.Result.Errors.Single().Contains("already exists")) throw new Exception("Existing blocked account must reject registration.");
var resend = await service.ResendConfirmationEmailAsync("blocked@example.test", default);
if (resend.Result.Succeeded || resend.ConfirmationToken != "") throw new Exception("Existing account must reject resend.");
var sender = new EmailSender();
var handlerType = typeof(RegisterUserCommand).Assembly.GetType("Application.Commands.Auth.RegisterUserCommandHandler")!;
var handler = (IRequestHandler<RegisterUserCommand, Result>)Activator.CreateInstance(handlerType, service, sender)!;
var result = await handler.Handle(new RegisterUserCommand("blocked@example.test", "Password123!"), default);
if (result.Succeeded || sender.Calls != 0) throw new Exception("Rejected registration must not send email.");
users.Exists = false;
login = await service.VerifyUserPasswordAsync("bad", "unknown@example.test", default);
if (login.Result.Succeeded || !login.Result.Errors.Single().Contains("Invalid email or password")) throw new Exception("Unknown login must keep generic error.");
Console.WriteLine("PASS: blocked login, duplicate registration, resend rejection, no email sent, unknown login.");

sealed class Users : UserManager<ApplicationUser> {
 public bool Exists = true;
 public Users() : base(DispatchProxy.Create<IUserStore<ApplicationUser>, ThrowProxy>(), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance) {}
 public override Task<ApplicationUser?> FindByEmailAsync(string email) => Task.FromResult<ApplicationUser?>(Exists ? new ApplicationUser {Id="blocked",Email="blocked@example.test",LockoutEnabled=true,LockoutEnd=DateTimeOffset.MaxValue} : null);
}
sealed class SignIn : SignInManager<ApplicationUser> {
 public SignIn(Users users) : base(users,new HttpContextAccessor(),DispatchProxy.Create<IUserClaimsPrincipalFactory<ApplicationUser>,ThrowProxy>(),Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),NullLogger<SignInManager<ApplicationUser>>.Instance,new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())),new DefaultUserConfirmation<ApplicationUser>()) {}
 public override Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user,string password,bool lockoutOnFailure) => Task.FromResult(SignInResult.LockedOut);
}
public class ThrowProxy : DispatchProxy { protected override object? Invoke(MethodInfo? method,object?[]? args) => throw new Exception("Unexpected dependency call: " + method?.Name); }
public class RedisProxy : DispatchProxy { protected override object? Invoke(MethodInfo? method,object?[]? args) => method?.Name=="GetDatabase" ? DispatchProxy.Create<IDatabase,ThrowProxy>() : throw new Exception("Unexpected Redis call"); }
sealed class EmailSender : IEmailSender { public int Calls; public Task SendConfirmationEmailAsync(string email,string token,CancellationToken cancellationToken) { Calls++;return Task.CompletedTask; } }

