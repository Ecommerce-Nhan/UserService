using Microsoft.AspNetCore.Identity;
using SharedLibrary.Helpers;
using System.Security.Claims;
using UserService.Domains.Entities;

namespace UserService.Infrastructure.Seeds;

public static class DefaultUsers
{
    public static async Task SeedBasicUserAsync(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        var defaultUser = new User
        {
            UserName = "basicuser@gmail.com",
            Email = "basicuser@gmail.com",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            IsActive = true,
            FirstName = "Basic",
            LastName = "User",
            PhoneNumber = "0858032268"
        };
        if (userManager.Users.All(u => u.Id != defaultUser.Id))
        {
            var user = await userManager.FindByEmailAsync(defaultUser.Email);
            if (user == null)
            {
                await userManager.CreateAsync(defaultUser, "123Pa$$word!");
                await userManager.AddToRoleAsync(defaultUser, Roles.Basic.ToString());
            }
        }
    }
    public static async Task SeedSuperAdminAsync(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        var defaultUser = new User
        {
            UserName = "superadmin@gmail.com",
            Email = "superadmin@gmail.com",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            IsActive = true,
            FirstName = "Admin",
            LastName = "User",
            PhoneNumber = "0858032268"
        };
        if (userManager.Users.All(u => u.Id != defaultUser.Id))
        {
            var user = await userManager.FindByEmailAsync(defaultUser.Email);
            if (user == null)
            {
                await userManager.CreateAsync(defaultUser, "123Pa$$word!");
                await userManager.AddToRoleAsync(defaultUser, Roles.Basic.ToString());
                await userManager.AddToRoleAsync(defaultUser, Roles.Admin.ToString());
                await userManager.AddToRoleAsync(defaultUser, Roles.SuperAdmin.ToString());
            }
            await roleManager.SeedClaimsForAdmin();
        }
    }
    private async static Task SeedClaimsForAdmin(this RoleManager<Role> roleManager)
    {
        var adminRole = await roleManager.FindByNameAsync("Admin");
        var superAdminRole = await roleManager.FindByNameAsync("SuperAdmin");
        if (superAdminRole is Role && adminRole is Role)
        {
            await roleManager.AddPermissionClaim(adminRole, "Product");
            await roleManager.AddPermissionClaim(superAdminRole, "Product");
            await roleManager.AddPermissionClaim(superAdminRole, "User");
            await roleManager.AddPermissionClaim(superAdminRole, "Role");
            await roleManager.AddPermissionClaim(superAdminRole, "RoleClaim");
        }
    }

    public static async Task AddPermissionClaim(this RoleManager<Role> roleManager, Role role, string module)
    {
        var allClaims = await roleManager.GetClaimsAsync(role);
        var allPermissions = GeneratePermissionsForModule(module);
        foreach (var permission in allPermissions)
        {
            if (!allClaims.Any(a => a.Type == "Permission" && a.Value == permission))
            {
                await roleManager.AddClaimAsync(role, new Claim("Permission", permission));
            }
        }
    }

    private static List<string> GeneratePermissionsForModule(string module)
    {
        return new List<string>()
        {
            $"Permissions.{module}.Create",
            $"Permissions.{module}.View",
            $"Permissions.{module}.Edit",
            $"Permissions.{module}.Delete",
        };
    }
}