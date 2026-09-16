using Application.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        //postgres config
        var connectionString = builder.Configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Database connection string was not found."); 
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        //identity config
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUser, CurrentUser>();

        //redis config
        var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Redis connection string was not found");
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnection);

            options.AbortOnConnectFail = false;
            options.ClientName = "CVManagementSystem";

            return ConnectionMultiplexer.Connect(options);
        });
        builder.Services.AddSingleton<ICacheService, RedisCacheService>();
        builder.Services.AddSingleton<IRecentAttributesCache, RecentAttributesCache>();
    }
}
