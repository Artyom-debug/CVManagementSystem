using Application.Interfaces;
using CloudinaryDotNet;
using Infrastructure.Auth;
using Infrastructure.Data;
using Infrastructure.Models;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using StackExchange.Redis;
using System.Security.Claims;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        AddDatabase(builder);
        AddIdentity(builder);
        AddRedis(builder);
        AddCloudinary(builder);
    }

    private static void AddDatabase(IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration
            .GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Database connection string was not found.");

        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(CreatePostgresConnectionString(connectionString)));

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<ApplicationDbContextInitializer>();
    }

    private static string CreatePostgresConnectionString(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
            throw new InvalidOperationException("PostgreSQL URI must contain a username and password.");

        var options = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        };

        if (uri.Query.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase))
            options.SslMode = SslMode.Require;

        return options.ConnectionString;
    }

    private static void AddIdentity(IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUser, CurrentUser>();

        builder.Services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 5;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
                options.ClaimsIdentity.EmailClaimType = ClaimTypes.Email;
                options.ClaimsIdentity.RoleClaimType = ClaimTypes.Role;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration was not found.");
        var signingKey = ValidateJwtOptions(jwtOptions);

        builder.Services.Configure<JwtOptions>(jwtSection);

        var authentication = builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSecurityStampAsync
                };
            });

        var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
        var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            authentication.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = "/api/auth/signin-google";
            });
        }

        var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
        var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];

        if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
        {
            authentication.AddFacebook(options =>
            {
                options.AppId = facebookAppId;
                options.AppSecret = facebookAppSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = "/api/auth/signin-facebook";
            });
        }

        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IIdentityService, IdentityService>();
        builder.Services.AddHttpClient<IEmailSender, EmailSender>();
        builder.Services.AddSingleton(TimeProvider.System);
    }

    private static async Task ValidateSecurityStampAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?
            .FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?
                .FindFirstValue(JwtRegisteredClaimNames.Sub);
        var tokenStamp = context.Principal?
            .FindFirstValue(TokenService.SecurityStampClaim);

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(tokenStamp))
        {
            context.Fail("The access token is missing required claims.");
            return;
        }

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = context.HttpContext.RequestServices
            .GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);

        if (user is null ||
            !await signInManager.CanSignInAsync(user) ||
            await userManager.IsLockedOutAsync(user))
        {
            context.Fail("The user cannot sign in.");
            return;
        }

        var currentStamp = await userManager.GetSecurityStampAsync(user);
        if (!string.Equals(tokenStamp, currentStamp, StringComparison.Ordinal))
            context.Fail("The access token has been revoked.");
    }

    private static byte[] ValidateJwtOptions(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            string.IsNullOrWhiteSpace(options.SecretKey) ||
            options.TokenValidityMins <= 0 ||
            options.RefreshTokenValidityDays <= 0)
        {
            throw new InvalidOperationException("JWT configuration is invalid.");
        }

        byte[] signingKey;
        try
        {
            signingKey = Convert.FromBase64String(options.SecretKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("JWT secret key must be a valid Base64 string.", exception);
        }

        if (signingKey.Length < 32)
        {
            throw new InvalidOperationException("JWT secret key must contain at least 32 bytes.");
        }

        return signingKey;
    }

    private static void AddRedis(IHostApplicationBuilder builder)
    {
        var redisConnection = builder.Configuration
            .GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string was not found.");

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = CreateRedisOptions(redisConnection);
            return ConnectionMultiplexer.Connect(options);
        });

        builder.Services.AddSingleton<ICacheService, RedisCacheService>();
        builder.Services.AddSingleton<IRecentAttributesCache, RecentAttributesCache>();
    }

    private static ConfigurationOptions CreateRedisOptions(string connectionString)
    {
        var isRedisUri =
            connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
            connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase);

        if (!isRedisUri)
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.ClientName = "CVManagementSystem";
            return options;
        }

        var uri = new Uri(connectionString);

        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2 || string.IsNullOrWhiteSpace(credentials[1]))
            throw new InvalidOperationException("Redis URI must contain a password.");

        var uriOptions = new ConfigurationOptions
        {
            User = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            Ssl = uri.Scheme == "rediss",
            AbortOnConnectFail = false,
            ClientName = "CVManagementSystem"
        };

        uriOptions.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);
        return uriOptions;
    }

    private static void AddCloudinary(IHostApplicationBuilder builder)
    {
        var section = builder.Configuration
            .GetSection(CloudinaryOptions.SectionName);

        builder.Services
            .AddOptions<CloudinaryOptions>()
            .Bind(section)
            .Validate(options => !string.IsNullOrWhiteSpace(options.CloudName), "Cloudinary cloud name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "Cloudinary API key is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiSecret), "Cloudinary API secret is required.")
            .Validate(options => options.DeliveryType is "upload" or "private" or "authenticated", "Unknown Cloudinary delivery type.")
            .ValidateOnStart();

        builder.Services.AddSingleton(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<CloudinaryOptions>>()
                .Value;

            var account = new Account(options.CloudName, options.ApiKey, options.ApiSecret);

            var cloudinary = new Cloudinary(account);

            cloudinary.Api.Secure = true;

            return cloudinary;
        });

        builder.Services.AddSingleton<IImageStorage, ImageStorage>();
    }
}
