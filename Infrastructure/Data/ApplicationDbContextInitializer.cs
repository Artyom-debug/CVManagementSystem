using Application.Constants;
using Domain.Enums;
using Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AttributeEntity = Domain.Entities.Attribute;

namespace Infrastructure.Data;

public sealed class ApplicationDbContextInitializer
{
    private readonly ILogger<ApplicationDbContextInitializer> _logger;
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ApplicationDbContextInitializer(ILogger<ApplicationDbContextInitializer> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration config)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _config = config;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        //seed roles
        var roles = new[] { Roles.Administrator, Roles.Candidate, Roles.Recruiter };
        foreach (var roleName in roles)
        {
            if (_roleManager.Roles.All(r => r.Name != roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        //seed admin
        var email = _config["AdminSeed:Email"];
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Administrator email was not configured.");

        email = email.Trim();
        var admin = await _userManager.FindByEmailAsync(email);

        if (admin is null)
        {
            var password = _config["AdminSeed:Password"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Administrator password was not configured.");

            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(admin, password);
            EnsureSucceeded(createResult, "Failed to create the administrator");
        }

        if (!await _userManager.IsInRoleAsync(admin, Roles.Administrator))
        {
            var addRoleResult = await _userManager.AddToRoleAsync(admin, Roles.Administrator);
            EnsureSucceeded(addRoleResult, "Failed to assign the administrator role");
        }

        var profileExists = await _context.Profiles
            .AnyAsync(profile => profile.UserId == admin.Id);

        if (!profileExists)
        {
            _context.Profiles.Add(new Domain.Entities.Profile(admin.Id));
            await _context.SaveChangesAsync();
        }

        //seed system attributes
        if(!_context.Attributes.Any())
        {
            var education = new AttributeEntity("Education Level", "The highest level of formal education achieved by the user", AttributeType.Dropdown, Category.Education, false);
            education.AddOptionsRange([
                "Primary education",
                "Secondary education",
                "Bachelor's Degree",
                "Master's Degree",
                "Doctoral Degree",
                "Vocational education"
                ]);
            education.MarkAsSystemAttribute();
            var definitions = new[]
            {
                new AttributeEntity("First Name", "The user's first name", AttributeType.String, Category.PersonalInformation, true),
                new AttributeEntity("Last Name", "The user's last name", AttributeType.String, Category.PersonalInformation, true),
                new AttributeEntity("Personal photo", "The user's profile photo", AttributeType.Image, Category.PersonalInformation, true),
                new AttributeEntity("Location", "Country and city the user is currently located in.", AttributeType.String, Category.PersonalInformation, true),
                new AttributeEntity("Birth Date", "The user's birth date", AttributeType.Date, Category.PersonalInformation, true),
                education
            };
            await _context.Attributes.AddRangeAsync(definitions);
            await _context.SaveChangesAsync();
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{operation}: {errors}");
    }
}
