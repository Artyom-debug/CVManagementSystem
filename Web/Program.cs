using Application;
using Application.Constants;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddApplicationServices();
builder.AddInfrastructureServices();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ManageAttributeLibrary, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManageSystemAttributes, policy => policy.RequireRole(Roles.Administrator));

    options.AddPolicy(Policies.ViewTagLibrary, policy => policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.ManageTagLibrary, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManageCandidateProfile, policy => policy.RequireRole(Roles.Candidate, Roles.Administrator));

    options.AddPolicy(Policies.ViewFullCandidateProfile, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManageCV, policy => policy.RequireRole(Roles.Candidate, Roles.Administrator));

    options.AddPolicy(Policies.LikeCV, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManagePositions, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ParticipateInDiscussions, policy => policy.RequireRole(Roles.Candidate, Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManageUsers, policy => policy.RequireRole(Roles.Administrator));
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
