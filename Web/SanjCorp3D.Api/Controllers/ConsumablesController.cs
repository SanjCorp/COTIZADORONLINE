using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Data;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Models;

namespace SanjCorp3D.Api.Controllers;

[ApiController, Authorize, Route("api/consumables")]
public sealed class ConsumablesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var query = db.Consumables.AsNoTracking();
        if (!includeArchived) query = query.Where(x => x.Active);
        return Ok(await query
            .OrderBy(x => x.Category)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.Material)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.Color)
            .ToListAsync(ct));
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Production}"), HttpPost]
    public async Task<IActionResult> Create(Consumable input, CancellationToken ct)
    {
        input.Id = 0;
        input.Active = true;
        NormalizeStock(input);
        Validate(input);
        await ClearDefault(input, ct);
        db.Consumables.Add(input);
        return await Save(input, ct);
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Production}"), HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, Consumable input, CancellationToken ct)
    {
        var item = await db.Consumables.FindAsync([id], ct);
        if (item is null) return NotFound();
        NormalizeStock(input);
        Validate(input);
        await ClearDefault(input, ct);
        item.Name = input.Name.Trim();
        item.Category = input.Category.Trim();
        item.Material = input.Material.Trim();
        item.Color = input.Color.Trim();
        item.PricePerUnit = input.PricePerUnit;
        item.Density = input.Density;
        item.IsDefault = input.IsDefault;
        item.Active = input.Active;
        item.StockGrams = input.StockGrams;
        item.LowStockGrams = input.LowStockGrams;
        SyncLegacyQuantity(item);
        return await Save(item, ct);
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Production}"), HttpPatch("{id:long}/stock")]
    public async Task<IActionResult> Stock(long id, [FromBody] StockRequest request, CancellationToken ct)
    {
        var item = await db.Consumables.FindAsync([id], ct);
        if (item is null) return NotFound();

        decimal total = request.StockGrams
            ?? (request.Quantity.HasValue ? request.Quantity.Value * 1000m : item.StockGrams);
        decimal threshold = request.LowStockGrams ?? item.LowStockGrams;
        if (total < 0 || threshold < 0)
            return BadRequest(new { message = "La existencia y el umbral no pueden ser negativos." });

        item.StockGrams = decimal.Round(total, 4, MidpointRounding.AwayFromZero);
        item.LowStockGrams = decimal.Round(threshold, 4, MidpointRounding.AwayFromZero);
        SyncLegacyQuantity(item);
        await db.SaveChangesAsync(ct);
        return Ok(item);
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Production}"), HttpPost("{id:long}/stock/add")]
    public async Task<IActionResult> AddStock(long id, [FromBody] StockAdditionRequest request, CancellationToken ct)
    {
        if (request.Kilograms < 0 || request.Grams < 0)
            return BadRequest(new { message = "Los kilos y gramos a agregar no pueden ser negativos." });

        var item = await db.Consumables.FindAsync([id], ct);
        if (item is null) return NotFound();
        item.StockGrams = decimal.Round(item.StockGrams + request.Kilograms * 1000m + request.Grams, 4, MidpointRounding.AwayFromZero);
        SyncLegacyQuantity(item);
        await db.SaveChangesAsync(ct);
        return Ok(item);
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Production}"), HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id, CancellationToken ct)
    {
        var item = await db.Consumables.FindAsync([id], ct);
        if (item is null) return NotFound();
        item.Active = false;
        item.IsDefault = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    public sealed record StockRequest(decimal? StockGrams, decimal? LowStockGrams, int? Quantity);
    public sealed record StockAdditionRequest(decimal Kilograms, decimal Grams);

    private async Task ClearDefault(Consumable input, CancellationToken ct)
    {
        if (input.IsDefault)
            await db.Consumables.Where(x => x.IsDefault).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false), ct);
    }

    private async Task<IActionResult> Save(Consumable item, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Ok(item);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Ya existe ese consumible, material y color." });
        }
    }

    private static void NormalizeStock(Consumable item)
    {
        // Older clients sent StockQuantity as a count of 1 kg spools.
        // Translate it only when the new total-grams field was not supplied.
        if (item.StockGrams <= 0 && item.StockQuantity > 0)
            item.StockGrams = item.StockQuantity * 1000m;
        item.StockGrams = decimal.Round(item.StockGrams, 4, MidpointRounding.AwayFromZero);
        item.LowStockGrams = decimal.Round(item.LowStockGrams, 4, MidpointRounding.AwayFromZero);
        SyncLegacyQuantity(item);
    }

    private static void SyncLegacyQuantity(Consumable item)
    {
        item.StockQuantity = item.StockGrams >= int.MaxValue * 1000m
            ? int.MaxValue
            : (int)decimal.Floor(item.StockGrams / 1000m);
    }

    private static void Validate(Consumable item)
    {
        if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Category) ||
            string.IsNullOrWhiteSpace(item.Material) || string.IsNullOrWhiteSpace(item.Color))
            throw new ArgumentException("Nombre, categoría, material y color son obligatorios.");
        if (item.PricePerUnit < 0 || item.Density <= 0 || item.StockGrams < 0 || item.LowStockGrams < 0)
            throw new ArgumentException("Precio, densidad, existencia o umbral no son válidos.");
    }
}
