using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Data;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Services;

namespace SanjCorp3D.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext db) : ControllerBase
{
    public sealed record LoginRequest(string Username, string Password, string? TwoFactorCode, bool RememberMe = false, string Workspace = "technology");
    public sealed record TwoFactorRequest(string Code);

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var identifier = request.Username.Trim();
        var user = await users.FindByNameAsync(identifier);
        if (user is null && identifier.Contains('@')) user = await users.FindByEmailAsync(identifier);
        if (user is null || !user.Active) return Unauthorized(new { message = "Credenciales incorrectas." });
        var roles = await users.GetRolesAsync(user);
        var isSuperAdmin = user.IsSupremeAdmin || roles.Contains(AppRoles.SuperAdmin);
        var tenant = user.TenantId.HasValue
            ? await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == user.TenantId.Value)
            : await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == TenantContext.TechnologyTenantId);
        if (tenant is null || !tenant.Active) return Unauthorized(new { message = "La cuenta pertenece a un espacio inactivo." });
        var workspace = request.Workspace.Trim().ToLowerInvariant();
        if (workspace is not ("technology" or "makers")) return BadRequest(new { message = "El espacio seleccionado no es válido." });
        if (!isSuperAdmin && workspace == "makers" && !roles.Contains(AppRoles.Maker)) return Unauthorized(new { message = "Esta cuenta no pertenece a Makers." });
        if (!isSuperAdmin && workspace == "technology" && roles.Contains(AppRoles.Maker)) return Unauthorized(new { message = "Esta cuenta pertenece a Makers." });
        var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode)) return Unauthorized(new { requiresTwoFactor = true });
            var recoveryCode = request.TwoFactorCode.Trim().Replace(" ", "");
            var authenticatorCode = recoveryCode.Replace("-", "");
            result = authenticatorCode.Length > 6
                ? await signIn.TwoFactorRecoveryCodeSignInAsync(recoveryCode)
                : await signIn.TwoFactorAuthenticatorSignInAsync(authenticatorCode, request.RememberMe, false);
        }
        if (result.IsLockedOut) return StatusCode(423, new { message = "Cuenta bloqueada temporalmente." });
        if (!result.Succeeded) return Unauthorized(new { message = "Credenciales incorrectas." });
        user.LastLoginAtUtc = DateTime.UtcNow;
        await users.UpdateAsync(user);
        return Ok(await Profile(user));
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return NoContent(); }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null || !user.Active ? Unauthorized() : Ok(await Profile(user));
    }

    [Authorize, HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var user = await users.GetUserAsync(User);
        if (user is null || !user.Active) return Unauthorized();
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }
        const string issuer = "SanjCorp3D";
        var label = Uri.EscapeDataString($"{issuer}:{user.UserName}");
        var uri = $"otpauth://totp/{label}?secret={key}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
        return Ok(new { sharedKey = key, authenticatorUri = uri });
    }

    [Authorize, HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null || !user.Active) return Unauthorized();
        var code = request.Code.Replace(" ", "").Replace("-", "");
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code))
            return BadRequest(new { message = "El código de autenticación no es válido." });
        await users.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        return Ok(new { enabled = true, recoveryCodes });
    }

    [Authorize, HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor()
    {
        var user = await users.GetUserAsync(User);
        if (user is null || !user.Active) return Unauthorized();
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        return NoContent();
    }

    private async Task<object> Profile(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var isSuperAdmin = user.IsSupremeAdmin || roles.Contains(AppRoles.SuperAdmin);
        var tenant = user.TenantId.HasValue
            ? await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == user.TenantId.Value)
            : await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == Services.TenantContext.TechnologyTenantId);
        var workspaces = isSuperAdmin
            ? new object[] { new { id = TenantContext.TechnologyTenantId, name = "SanjCorp Technology", kind = "technology" }, new { id = (Guid?)null, name = "Makers", kind = "makers" } }
            : new object[] { new { id = tenant?.Id, name = tenant?.Name ?? "", kind = tenant?.Kind ?? "technology" } };
        return new
        {
            user.Id, user.UserName, user.Email, user.DisplayName, user.TenantId,
            TenantName = tenant?.Name, TenantKind = tenant?.Kind, LogoUrl = tenant?.LogoUrl,
            IsSuperAdmin = isSuperAdmin, TwoFactorEnabled = await users.GetTwoFactorEnabledAsync(user), Roles = roles,
            Workspaces = workspaces
        };
    }
}
