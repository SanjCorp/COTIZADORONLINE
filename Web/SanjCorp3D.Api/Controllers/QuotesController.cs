using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Contracts;
using SanjCorp3D.Api.Data;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Models;
using SanjCorp3D.Api.Services;

namespace SanjCorp3D.Api.Controllers;

[ApiController,Authorize,Route("api/quotes")]
public sealed class QuotesController(AppDbContext db,BusinessSettingsService settings):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult>List([FromQuery]string search="",[FromQuery]DateTime? from=null,[FromQuery]DateTime? to=null,CancellationToken ct=default)
    {
        var q=db.Quotes.AsNoTracking().Include(x=>x.Sale).AsQueryable();
        search=search.Trim();if(search.Length>0)q=q.Where(x=>EF.Functions.ILike(x.OrderCode,$"%{search}%")||EF.Functions.ILike(x.Customer,$"%{search}%")||EF.Functions.ILike(x.ProjectName,$"%{search}%"));
        if(from.HasValue)q=q.Where(x=>x.CreatedAtUtc>=DateTime.SpecifyKind(from.Value.Date,DateTimeKind.Utc));
        if(to.HasValue)q=q.Where(x=>x.CreatedAtUtc<DateTime.SpecifyKind(to.Value.Date.AddDays(1),DateTimeKind.Utc));
        var rows=await q.OrderByDescending(x=>x.CreatedAtUtc).Select(x=>new{x.Id,x.OrderCode,x.CreatedAtUtc,x.Customer,x.ProjectName,x.PrinterName,x.TotalWeight,CostTotal=x.Subtotal,x.RecommendedPrice,SoldAtUtc=x.Sale==null?(DateTime?)null:x.Sale.SoldAtUtc}).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult>Get(long id,CancellationToken ct)
    {
        var q=await db.Quotes.AsNoTracking().AsSplitQuery().Where(x=>x.Id==id).Select(x=>new
        {
            x.Id,x.OrderCode,x.CreatedAtUtc,x.Customer,x.ProjectName,x.PrinterName,x.PrintHours,x.Quantity,
            x.AdditionalManualCost,x.ProfitMultiplier,x.Notes,x.TotalWeight,x.MaterialCost,x.ElectricityCost,
            x.MachineCost,x.MaintenanceCost,x.LaborCost,x.AdditionalCost,x.FunctionalSurcharge,x.Subtotal,
            x.ProfitAmount,x.TaxAmount,x.RecommendedPrice,
            Consumables=x.Consumables.OrderBy(v=>v.Id).Select(v=>new{v.Id,v.LegacyConsumableId,v.Name,v.Category,v.Material,v.Color,v.Grams,v.PricePerUnit,v.Density,v.LineCost}).ToList(),
            Materials=x.Materials.OrderBy(v=>v.Id).Select(v=>new{v.Id,v.LegacyMaterialId,v.Name,v.Quantity,v.UnitPrice,v.LineCost}).ToList(),
            Sale=x.Sale==null?null:new{x.Sale.Id,x.Sale.SoldAtUtc,x.Sale.SaleAmount}
        }).FirstOrDefaultAsync(ct);
        return q is null?NotFound():Ok(q);
    }

    [Authorize(Roles=$"{AppRoles.Administrator},{AppRoles.Sales},{AppRoles.Maker},{AppRoles.SuperAdmin}"),HttpPost("calculate")]
    public async Task<IActionResult>Calculate(QuoteRequest request,CancellationToken ct)
    {
        var resolved=await Resolve(request,ct);return Ok(QuoteCalculator.Calculate(request,resolved.Printer,resolved.Consumables,resolved.Materials,await settings.GetAsync(ct)));
    }

    [Authorize(Roles=$"{AppRoles.Administrator},{AppRoles.Sales},{AppRoles.Maker},{AppRoles.SuperAdmin}"),HttpPost]
    public async Task<IActionResult>Create(QuoteRequest request,CancellationToken ct)
    {
        var resolved=await Resolve(request,ct);var calculation=QuoteCalculator.Calculate(request,resolved.Printer,resolved.Consumables,resolved.Materials,await settings.GetAsync(ct));
        var strategy=db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            long next=(await db.Quotes.MaxAsync(x=>(long?)x.Id,ct)??0)+1;var created=DateTime.UtcNow;
            var quote=new Quote{OrderCode=$"{Initial(request.Customer)}{Initial(resolved.Printer.Name)}{created:yyyyMMdd}{next:0000}",CreatedAtUtc=created,Customer=request.Customer.Trim(),ProjectName=request.ProjectName.Trim(),PrinterName=resolved.Printer.Name,PrintHours=request.PrintHours,Quantity=request.Quantity,AdditionalManualCost=request.AdditionalManualCost,ProfitMultiplier=request.ProfitMultiplier,Notes=request.Notes?.Trim()??string.Empty,TotalWeight=calculation.TotalWeight,MaterialCost=calculation.MaterialCost,ElectricityCost=calculation.ElectricityCost,MachineCost=0,MaintenanceCost=calculation.MaintenanceCost,LaborCost=0,AdditionalCost=calculation.AdditionalCost,FunctionalSurcharge=0,Subtotal=calculation.Subtotal,ProfitAmount=calculation.ProfitAmount,TaxAmount=calculation.TaxAmount,RecommendedPrice=calculation.RecommendedPrice};
            foreach(var line in resolved.Consumables)quote.Consumables.Add(new QuoteConsumable{LegacyConsumableId=line.Item.Id,Name=line.Item.Name,Category=line.Item.Category,Material=line.Item.Material,Color=line.Item.Color,Grams=line.Grams,PricePerUnit=line.Item.PricePerUnit,Density=line.Item.Density,LineCost=decimal.Round(line.UnitCost*request.Quantity,4,MidpointRounding.AwayFromZero)});
            foreach(var line in resolved.Materials)quote.Materials.Add(new QuoteMaterial{LegacyMaterialId=line.Item.Id,Name=line.Item.Name,Quantity=line.Quantity,UnitPrice=line.Item.UnitPrice,LineCost=decimal.Round(line.Cost,4,MidpointRounding.AwayFromZero)});
            db.Quotes.Add(quote);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
            return CreatedAtAction(nameof(Get),new{id=quote.Id},new{quote.Id,quote.OrderCode,quote.CreatedAtUtc,quote.Customer,quote.ProjectName,quote.PrinterName,quote.TotalWeight,CostTotal=quote.Subtotal,quote.RecommendedPrice,SoldAtUtc=(DateTime?)null});
        });
    }

    [Authorize(Roles=$"{AppRoles.Administrator},{AppRoles.Sales},{AppRoles.Maker},{AppRoles.SuperAdmin}"),HttpPost("{id:long}/sale")]
    public async Task<IActionResult>ConfirmSale(long id,CancellationToken ct)
    {
        var strategy=db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var quote = await db.Quotes
                .Include(x => x.Sale)
                .Include(x => x.Consumables)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (quote is null) return NotFound();
            if (quote.Sale is not null) return Conflict(new { message = "Esta cotización ya fue confirmada como venta." });

            var usage = new List<(long Id, decimal Grams)>();
            foreach (var line in quote.Consumables)
            {
                if (line.Grams <= 0) continue;
                if (line.LegacyConsumableId <= 0)
                    return Conflict(new { message = $"La cotización {quote.OrderCode} tiene un consumible sin referencia de inventario." });
                var grams = decimal.Round(line.Grams * quote.Quantity, 4, MidpointRounding.AwayFromZero);
                var index = usage.FindIndex(x => x.Id == line.LegacyConsumableId);
                if (index < 0) usage.Add((line.LegacyConsumableId, grams));
                else usage[index] = (usage[index].Id, usage[index].Grams + grams);
            }

            var ids = usage.Select(x => x.Id).ToArray();
            var inventory = await db.Consumables.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            foreach (var required in usage)
            {
                if (!inventory.TryGetValue(required.Id, out var item))
                    return Conflict(new { message = "Uno de los filamentos de esta cotización ya no existe en el inventario." });
                if (item.StockGrams + 0.0001m < required.Grams)
                    return Conflict(new
                    {
                        code = "INSUFFICIENT_STOCK",
                        message = $"Stock insuficiente para {item.Name} · {item.Material} · {item.Color}. Disponible: {FormatWeight(item.StockGrams)}; necesario: {FormatWeight(required.Grams)}."
                    });
            }

            var sale = new Sale { SoldAtUtc = DateTime.UtcNow, SaleAmount = quote.RecommendedPrice, CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) is string value && Guid.TryParse(value, out var userId) ? userId : null };
            foreach (var required in usage)
            {
                var item = inventory[required.Id];
                item.StockGrams = decimal.Round(item.StockGrams - required.Grams, 4, MidpointRounding.AwayFromZero);
                SyncLegacyQuantity(item);
                sale.Consumables.Add(new SaleConsumable { ConsumableId = item.Id, Grams = required.Grams });
            }
            quote.Sale = sale;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Ok(new
            {
                sale.Id,
                sale.QuoteId,
                sale.SoldAtUtc,
                sale.SaleAmount,
                consumed = usage.Select(x => new { consumableId = x.Id, grams = x.Grams }).ToList()
            });
        });
    }

    [Authorize(Roles=$"{AppRoles.Administrator},{AppRoles.Maker},{AppRoles.SuperAdmin}"),HttpDelete("{id:long}")]
    public async Task<IActionResult>Delete(long id,CancellationToken ct)
    {
        var strategy=db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var quote = await db.Quotes.Include(x => x.Sale).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (quote is null) return NotFound();
            if (quote.Sale is not null)
            {
                var consumption = await db.SaleConsumables.Where(x => x.SaleId == quote.Sale.Id).ToListAsync(ct);
                var ids = consumption.Select(x => x.ConsumableId).Distinct().ToArray();
                var inventory = await db.Consumables.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
                foreach (var line in consumption)
                {
                    if (!inventory.TryGetValue(line.ConsumableId, out var item)) continue;
                    item.StockGrams = decimal.Round(item.StockGrams + line.Grams, 4, MidpointRounding.AwayFromZero);
                    SyncLegacyQuantity(item);
                }
            }
            db.Quotes.Remove(quote);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return NoContent();
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult>Export([FromQuery]string search="",[FromQuery]DateTime? from=null,[FromQuery]DateTime? to=null,CancellationToken ct=default)
    {
        var q=db.Quotes.AsNoTracking().Include(x=>x.Sale).AsQueryable();search=search.Trim();if(search.Length>0)q=q.Where(x=>EF.Functions.ILike(x.OrderCode,$"%{search}%")||EF.Functions.ILike(x.Customer,$"%{search}%")||EF.Functions.ILike(x.ProjectName,$"%{search}%"));if(from.HasValue)q=q.Where(x=>x.CreatedAtUtc>=DateTime.SpecifyKind(from.Value.Date,DateTimeKind.Utc));if(to.HasValue)q=q.Where(x=>x.CreatedAtUtc<DateTime.SpecifyKind(to.Value.Date.AddDays(1),DateTimeKind.Utc));
        var rows=await q.OrderByDescending(x=>x.CreatedAtUtc).ToListAsync(ct);var csv=new StringBuilder("Codigo,Estado,Fecha,Cliente,Proyecto,Impresora,Peso,Costo,Precio\r\n");foreach(var x in rows)csv.AppendLine(string.Join(',',Csv(x.OrderCode),Csv(x.Sale is null?"COTIZACION":"VENDIDA"),Csv(x.CreatedAtUtc.ToString("O")),Csv(x.Customer),Csv(x.ProjectName),Csv(x.PrinterName),x.TotalWeight.ToString(CultureInfo.InvariantCulture),x.Subtotal.ToString(CultureInfo.InvariantCulture),x.RecommendedPrice.ToString(CultureInfo.InvariantCulture)));return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),"text/csv",$"cotizaciones-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private async Task<(Printer Printer,List<QuoteCalculator.ConsumableLine> Consumables,List<QuoteCalculator.MaterialLine> Materials)>Resolve(QuoteRequest request,CancellationToken ct)
    {
        var printer=await db.Printers.FirstOrDefaultAsync(x=>x.Id==request.PrinterId&&x.Active&&x.IsDefault,ct)??throw new ArgumentException("Selecciona una impresora favorita activa antes de cotizar.");
        var consumableIds=request.Consumables.Select(x=>x.ConsumableId).Distinct().ToArray();var available=await db.Consumables.Where(x=>consumableIds.Contains(x.Id)&&x.Active).ToDictionaryAsync(x=>x.Id,ct);var consumables=new List<QuoteCalculator.ConsumableLine>();foreach(var input in request.Consumables){if(!available.TryGetValue(input.ConsumableId,out var item))throw new ArgumentException("Uno de los consumibles no existe o está archivado.");if(item.StockGrams<=0)throw new ArgumentException($"No hay existencia de {item.Name} · {item.Material} · {item.Color}.");consumables.Add(new(item,input.Grams));}
        var materialIds=request.Materials.Select(x=>x.MaterialId).Distinct().ToArray();var materialCatalog=await db.Materials.Where(x=>materialIds.Contains(x.Id)&&x.Active).ToDictionaryAsync(x=>x.Id,ct);var materials=new List<QuoteCalculator.MaterialLine>();foreach(var input in request.Materials){if(!materialCatalog.TryGetValue(input.MaterialId,out var item))throw new ArgumentException("Uno de los materiales no existe o está archivado.");materials.Add(new(item,input.Quantity));}
        return(printer,consumables,materials);
    }
    private static void SyncLegacyQuantity(Consumable item)
    {
        item.StockQuantity = item.StockGrams >= int.MaxValue * 1000m
            ? int.MaxValue
            : (int)decimal.Floor(item.StockGrams / 1000m);
    }
    private static string FormatWeight(decimal grams)
    {
        var kilos = decimal.Floor(grams / 1000m);
        var remainder = grams - kilos * 1000m;
        return kilos > 0 && remainder > 0 ? $"{kilos:0.##} kg {remainder:0.##} g" : kilos > 0 ? $"{kilos:0.##} kg" : $"{remainder:0.##} g";
    }
    private static string Initial(string value){foreach(char c in value.Trim().Normalize(System.Text.NormalizationForm.FormD)){if(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark||!char.IsLetterOrDigit(c))continue;return char.ToUpperInvariant(c).ToString();}return"X";}
    private static string Csv(string value)=>$"\"{value.Replace("\"","\"\"")}\"";
}
