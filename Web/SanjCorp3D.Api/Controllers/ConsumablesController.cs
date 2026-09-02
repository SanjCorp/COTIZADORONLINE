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
    public async Task<IActionResult> List([FromQuery] bool includeArchived=false,CancellationToken ct=default){var q=db.Consumables.AsNoTracking();if(!includeArchived)q=q.Where(x=>x.Active);return Ok(await q.OrderBy(x=>x.Category).ThenByDescending(x=>x.IsDefault).ThenBy(x=>x.Material).ThenBy(x=>x.Name).ThenBy(x=>x.Color).ToListAsync(ct));}

    [Authorize(Roles=$"{AppRoles.Administrator},{AppRoles.Production}"),HttpPost]
    public async Task<IActionResult>Create(Consumable input,CancellationToken ct){input.Id=0;input.Active=true;Validate(input);await ClearDefault(input,ct);db.Consumables.Add(input);return await Save(input,ct);}

    [Authorize(Roles=$"{AppRoles.Administrator},{AppRoles.Production}"),HttpPut("{id:long}")]
    public async Task<IActionResult>Update(long id,Consumable input,CancellationToken ct){var item=await db.Consumables.FindAsync([id],ct);if(item is null)return NotFound();Validate(input);await ClearDefault(input,ct);item.Name=input.Name.Trim();item.Category=input.Category.Trim();item.Material=input.Material.Trim();item.Color=input.Color.Trim();item.PricePerUnit=input.PricePerUnit;item.Density=input.Density;item.IsDefault=input.IsDefault;item.Active=input.Active;item.StockQuantity=input.StockQuantity;return await Save(item,ct);}

    [Authorize(Roles=$"{AppRoles.Administrator},{AppRoles.Production}"),HttpPatch("{id:long}/stock")]
    public async Task<IActionResult>Stock(long id,[FromBody]StockRequest request,CancellationToken ct){if(request.Quantity<0)return BadRequest(new{message="La existencia no puede ser negativa."});var item=await db.Consumables.FindAsync([id],ct);if(item is null)return NotFound();item.StockQuantity=request.Quantity;await db.SaveChangesAsync(ct);return Ok(item);}

    [Authorize(Roles=$"{AppRoles.Administrator},{AppRoles.Production}"),HttpDelete("{id:long}")]
    public async Task<IActionResult>Archive(long id,CancellationToken ct){var item=await db.Consumables.FindAsync([id],ct);if(item is null)return NotFound();item.Active=false;item.IsDefault=false;await db.SaveChangesAsync(ct);return NoContent();}

    public sealed record StockRequest(int Quantity);
    private async Task ClearDefault(Consumable input,CancellationToken ct){if(input.IsDefault)await db.Consumables.Where(x=>x.IsDefault).ExecuteUpdateAsync(x=>x.SetProperty(p=>p.IsDefault,false),ct);}
    private async Task<IActionResult>Save(Consumable item,CancellationToken ct){try{await db.SaveChangesAsync(ct);return Ok(item);}catch(DbUpdateException){return Conflict(new{message="Ya existe ese consumible, material y color."});}}
    private static void Validate(Consumable x){if(string.IsNullOrWhiteSpace(x.Name)||string.IsNullOrWhiteSpace(x.Category)||string.IsNullOrWhiteSpace(x.Material)||string.IsNullOrWhiteSpace(x.Color))throw new ArgumentException("Nombre, categoría, material y color son obligatorios.");if(x.PricePerUnit<0||x.Density<=0||x.StockQuantity<0)throw new ArgumentException("Precio, densidad o inventario no son válidos.");}
}
