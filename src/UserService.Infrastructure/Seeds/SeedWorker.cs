using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UserService.Domains.Entities;

namespace UserService.Infrastructure.Seeds;

public class SeedWorker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public SeedWorker(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("app");

        try
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

            await Seeds.DefaultRoles.SeedAsync(userManager, roleManager);
            await Seeds.DefaultUsers.SeedBasicUserAsync(userManager, roleManager);
            await Seeds.DefaultUsers.SeedSuperAdminAsync(userManager, roleManager);
            await SeedRoleToCache(roleManager);
            logger.LogInformation("Finished Seeding Default Data");
            logger.LogInformation("Application Starting");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An error occurred seeding the DB");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    private async Task SeedRoleToCache(RoleManager<Role> roleManager)
    {
        var cache = _serviceProvider.GetRequiredService<IDistributedCache>();
        var roles = roleManager.Roles.ToList();
        foreach (var role in roles)
        {
            var claims = await roleManager.GetClaimsAsync(role);
            var permissions = claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToHashSet();

            await cache.SetStringAsync(
                $"role_permissions:{role.Name}",
                JsonSerializer.Serialize(permissions)
            );
        }
    }

}