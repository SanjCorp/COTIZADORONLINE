using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Data;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Models;

namespace SanjCorp3D.Api.Controllers;

[ApiController, Authorize, Route("api/printers")]
public sealed class PrintersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var query = db.Printers.AsNoTracking();
        if (!includeArchived) query = query.Where(x => x.Active);
        return Ok(await query.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToListAsync(ct));
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Production}"), HttpPost]
    public async Task<IActionResult> Create(Printer input, CancellationToken ct)
    {
        input.Id = 0; input.Active = true; Validate(input);
        await ClearDefault(input, ct); db.Printers.Add(input);
        return await Save(input, ct);
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Production}"), HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, Printer input, CancellationToken ct)
    {
        var item = await db.Printers.FindAsync([id], ct); if (item is null) return NotFound();
        Validate(input); await ClearDefault(input, ct);
        item.Name=input.Name.Trim(); item.BuildX=input.BuildX; item.BuildY=input.BuildY; item.BuildZ=input.BuildZ; item.Nozzle=input.Nozzle;
        item.Speed=input.Speed; item.PowerWatts=input.PowerWatts; item.HourlyCost=input.HourlyCost; item.IsDefault=input.IsDefault; item.Active=input.Active;
        return await Save(item, ct);
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Production}"), HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id, CancellationToken ct) { var item=await db.Printers.FindAsync([id],ct); if(item is null)return NotFound(); item.Active=false; item.IsDefault=false; await db.SaveChangesAsync(ct); return NoContent(); }

    private async Task ClearDefault(Printer input, CancellationToken ct) { if(input.IsDefault) await db.Printers.Where(x=>x.IsDefault).ExecuteUpdateAsync(x=>x.SetProperty(p=>p.IsDefault,false),ct); }
    private async Task<IActionResult> Save(Printer item, CancellationToken ct) { try { await db.SaveChangesAsync(ct); return Ok(item); } catch(DbUpdateException){ return Conflict(new{message="Ya existe una impresora con ese nombre."}); } }
    private static void Validate(Printer item) { if(string.IsNullOrWhiteSpace(item.Name)||item.Name.Trim().Length>100)throw new ArgumentException("El nombre es obligatorio y admite hasta 100 caracteres."); if(item.BuildX<=0||item.BuildY<=0||item.BuildZ<=0||item.Nozzle<=0||item.Speed<=0||item.PowerWatts<0||item.HourlyCost<0)throw new ArgumentException("Revisa dimensiones, boquilla, velocidad, potencia y costo."); }
}
