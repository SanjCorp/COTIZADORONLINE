using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Contracts;
using SanjCorp3D.Api.Data;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Models;

namespace SanjCorp3D.Api.Controllers;

[ApiController, Authorize, Route("api/products")]
public sealed class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var query = db.ProductCatalogs.AsNoTracking();
        if (!includeArchived) query = query.Where(x => x.Active);
        return Ok(await query.OrderBy(x => x.Name).Select(x => new ProductCatalogDto(x.Id, x.Name, x.Active)).ToListAsync(ct));
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Maker},{AppRoles.SuperAdmin}"), HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "El nombre del producto es obligatorio." });
        var item = new ProductCatalog { Name = name, Active = true };
        db.ProductCatalogs.Add(item);
        try { await db.SaveChangesAsync(ct); return Ok(new ProductCatalogDto(item.Id, item.Name, item.Active)); }
        catch (DbUpdateException) { return Conflict(new { message = "Ese producto ya está registrado." }); }
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Maker},{AppRoles.SuperAdmin}"), HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(long id, CreateProductRequest request, CancellationToken ct)
    {
        var item = await db.ProductCatalogs.FindAsync([id], ct);
        if (item is null) return NotFound();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "El nombre del producto es obligatorio." });
        item.Name = name;
        try { await db.SaveChangesAsync(ct); return Ok(new ProductCatalogDto(item.Id, item.Name, item.Active)); }
        catch (DbUpdateException) { return Conflict(new { message = "Ese producto ya está registrado." }); }
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Maker},{AppRoles.SuperAdmin}"), HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id, CancellationToken ct)
    {
        var item = await db.ProductCatalogs.FindAsync([id], ct);
        if (item is null) return NotFound();
        item.Active = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
