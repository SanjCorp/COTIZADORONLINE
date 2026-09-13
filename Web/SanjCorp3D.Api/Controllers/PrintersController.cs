using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Data;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Models;
using SanjCorp3D.Api.Services;

namespace SanjCorp3D.Api.Controllers;

[ApiController, Authorize, Route("api/printers")]
public sealed class PrintersController(AppDbContext db, TenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        IQueryable<Printer> query;
        if (User.IsInRole(AppRoles.SuperAdmin))
        {
            query = db.Printers.IgnoreQueryFilters().AsNoTracking()
                .GroupBy(x => x.CatalogId)
                .Select(x => x.OrderBy(item => item.Id).First());
        }
        else query = db.Printers.AsNoTracking();

        if (!includeArchived) query = query.Where(x => x.Active);
        return Ok(await query.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToListAsync(ct));
    }

    [Authorize(Roles = AppRoles.SuperAdmin), HttpPost]
    public async Task<IActionResult> Create(Printer input, CancellationToken ct)
    {
        input.Id = 0;
        input.Active = true;
        input.IsDefault = false;
        Validate(input);
        if (await NameExists(input.Name, null, ct)) return Conflict(new { message = "Ya existe una impresora con ese nombre en el catálogo global." });

        var catalogId = Guid.NewGuid();
        var tenantIds = await db.Tenants.IgnoreQueryFilters().Select(x => x.Id).ToListAsync(ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            Printer? first = null;
            foreach (var tenantId in tenantIds)
            {
                tenantContext.Use(tenantId);
                var item = Copy(input, catalogId);
                db.Printers.Add(item);
                await db.SaveChangesAsync(ct);
                first ??= item;
            }
            await transaction.CommitAsync(ct);
            return Ok(first ?? input);
        });
    }

    [Authorize(Roles = AppRoles.SuperAdmin), HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, Printer input, CancellationToken ct)
    {
        Validate(input);
        var source = await db.Printers.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (source is null) return NotFound();
        if (await NameExists(input.Name, source.CatalogId, ct)) return Conflict(new { message = "Ya existe una impresora con ese nombre en el catálogo global." });

        await db.Printers.IgnoreQueryFilters().Where(x => x.CatalogId == source.CatalogId).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Name, input.Name.Trim())
            .SetProperty(x => x.BuildX, input.BuildX)
            .SetProperty(x => x.BuildY, input.BuildY)
            .SetProperty(x => x.BuildZ, input.BuildZ)
            .SetProperty(x => x.Nozzle, input.Nozzle)
            .SetProperty(x => x.Speed, input.Speed)
            .SetProperty(x => x.PowerWatts, input.PowerWatts)
            .SetProperty(x => x.HourlyCost, input.HourlyCost)
            .SetProperty(x => x.Active, input.Active), ct);
        if (!input.Active)
            await db.Printers.IgnoreQueryFilters().Where(x => x.CatalogId == source.CatalogId)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false), ct);
        return Ok(input);
    }

    [Authorize(Roles = AppRoles.SuperAdmin), HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id, CancellationToken ct)
    {
        var source = await db.Printers.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (source is null) return NotFound();
        await db.Printers.IgnoreQueryFilters().Where(x => x.CatalogId == source.CatalogId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Active, false).SetProperty(p => p.IsDefault, false), ct);
        return NoContent();
    }

    [HttpPut("{id:long}/favorite")]
    public async Task<IActionResult> Favorite(long id, FavoritePrinterRequest request, CancellationToken ct)
    {
        if (User.IsInRole(AppRoles.SuperAdmin)) return BadRequest(new { message = "Selecciona las favoritas desde una cuenta de trabajo." });
        var item = await db.Printers.FirstOrDefaultAsync(x => x.Id == id && x.Active, ct);
        if (item is null) return NotFound();
        item.IsDefault = request.Favorite;
        await db.SaveChangesAsync(ct);
        return Ok(item);
    }

    private async Task<bool> NameExists(string name, Guid? exceptCatalogId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        return await db.Printers.IgnoreQueryFilters().AnyAsync(x => x.Name.ToLower() == normalized && (!exceptCatalogId.HasValue || x.CatalogId != exceptCatalogId.Value), ct);
    }

    private static Printer Copy(Printer source, Guid catalogId) => new()
    {
        CatalogId = catalogId,
        Name = source.Name.Trim(),
        BuildX = source.BuildX,
        BuildY = source.BuildY,
        BuildZ = source.BuildZ,
        Nozzle = source.Nozzle,
        Speed = source.Speed,
        PowerWatts = source.PowerWatts,
        HourlyCost = source.HourlyCost,
        IsDefault = false,
        Active = source.Active
    };

    private static void Validate(Printer item)
    {
        if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 100)
            throw new ArgumentException("El nombre es obligatorio y admite hasta 100 caracteres.");
        if (item.BuildX <= 0 || item.BuildY <= 0 || item.BuildZ <= 0 || item.Nozzle <= 0 || item.Speed <= 0 || item.PowerWatts < 0 || item.HourlyCost < 0)
            throw new ArgumentException("Revisa dimensiones, boquilla, velocidad, potencia y costo.");
    }
}

public sealed record FavoritePrinterRequest(bool Favorite);
