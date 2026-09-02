using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Data;
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
        await DatabaseSequenceService.AlignBusinessSequencesAsync(db);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));

        var username = configuration["BootstrapAdmin:Username"];
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return;

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByNameAsync(username) is not null) return;
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? $"{username.Trim()}@local.sanjcorp3d" : email.Trim();
        var admin = new ApplicationUser
        {
            UserName = username.Trim(),
            Email = normalizedEmail,
            DisplayName = "Richi Sanj Flo",
            EmailConfirmed = true
        };
        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
        await users.AddToRoleAsync(admin, AppRoles.Administrator);
    }
}
