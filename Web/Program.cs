using Application;
using Application.Constants;
using Infrastructure;
using Infrastructure.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using Web.ExceptionHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedHost |
                               ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CV Management System API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter the JWT access token.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddApplicationServices();
builder.AddInfrastructureServices();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ManageAttributeLibrary, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManageSystemAttributes, policy => policy.RequireRole(Roles.Administrator));

    options.AddPolicy(Policies.ViewTagLibrary, policy => policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.ManageTagLibrary, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManagePersonalProfile, policy => policy.RequireRole(Roles.Candidate, Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManageCandidateProfile, policy => policy.RequireRole(Roles.Candidate, Roles.Administrator));

    options.AddPolicy(Policies.ViewFullCandidateProfile, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManageCV, policy => policy.RequireRole(Roles.Candidate, Roles.Administrator));

    options.AddPolicy(Policies.LikeCV, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.DeleteCV, policy => policy.RequireRole(Roles.Administrator));

    options.AddPolicy(Policies.ManagePositions, policy => policy.RequireRole(Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ParticipateInDiscussions, policy => policy.RequireRole(Roles.Candidate, Roles.Recruiter, Roles.Administrator));

    options.AddPolicy(Policies.ManageUsers, policy => policy.RequireRole(Roles.Administrator));
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();

    await initialiser.InitializeAsync();
    await initialiser.SeedAsync();
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CV Management System API v1");
    });
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
if (app.Configuration.GetValue("UseHttpsRedirection", true))
    app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indexFile = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
    if (!indexFile.Exists)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexFile);
});

app.Run();
