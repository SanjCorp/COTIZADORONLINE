using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Data;
using SanjCorp3D.Api.Models;
using SanjCorp3D.Api.Services;

namespace SanjCorp3D.Api.Identity;

public static class IdentitySeeder
{
    public static async Task EnsureSeededAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Database.CanConnectAsync()) return;
        await db.Database.MigrateAsync();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        var technology = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == TenantContext.TechnologyTenantId);
        if (technology is null)
        {
            technology = new Tenant
            {
                Id = TenantContext.TechnologyTenantId,
                Name = "SanjCorp Technology",
                Slug = "sanjcorp-technology",
                Kind = "technology",
                Active = true
            };
            db.Tenants.Add(technology);
            await db.SaveChangesAsync();
        }
        tenantContext.Use(technology.Id);
        await DatabaseSequenceService.AlignBusinessSequencesAsync(db);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));

        var username = configuration["BootstrapAdmin:Username"];
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string supremeUsername = "richi_sanj_flo";
        if (!string.Equals(username?.Trim(), supremeUsername, StringComparison.OrdinalIgnoreCase)) username = supremeUsername;
        if (string.IsNullOrWhiteSpace(password))
        {
            await AssignExistingUsersToTechnology(users, technology.Id);
            return;
        }

        var existing = await users.FindByNameAsync(username!);
        if (existing is not null)
        {
            existing.TenantId = null;
            existing.IsSupremeAdmin = true;
            await users.UpdateAsync(existing);
            var currentRoles = await users.GetRolesAsync(existing);
            if (currentRoles.Any()) await users.RemoveFromRolesAsync(existing, currentRoles);
            await users.AddToRoleAsync(existing, AppRoles.SuperAdmin);
            await AssignExistingUsersToTechnology(users, technology.Id);
            return;
        }
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? $"{username!.Trim()}@local.sanjcorp3d" : email.Trim();
        var admin = new ApplicationUser
        {
            UserName = username!.Trim(),
            Email = normalizedEmail,
            DisplayName = "Richi Sanj Flo",
            EmailConfirmed = true,
            TenantId = null
            ,IsSupremeAdmin = true
        };
        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
        await users.AddToRoleAsync(admin, AppRoles.SuperAdmin);
        await AssignExistingUsersToTechnology(users, technology.Id);
    }

    private static async Task AssignExistingUsersToTechnology(UserManager<ApplicationUser> users, Guid technologyId)
    {
        foreach (var user in users.Users.Where(x => x.TenantId == null).ToList())
        {
            var roles = await users.GetRolesAsync(user);
            if (roles.Contains(AppRoles.SuperAdmin)) continue;
            user.TenantId = technologyId;
            await users.UpdateAsync(user);
        }
    }
}
