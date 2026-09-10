namespace SanjCorp3D.Api.Models;

public interface IActiveEntity { bool Active { get; set; } }

public sealed class Printer : IActiveEntity
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public decimal BuildX { get; set; }
    public decimal BuildY { get; set; }
    public decimal BuildZ { get; set; }
    public decimal Nozzle { get; set; }
    public decimal Speed { get; set; }
    public decimal PowerWatts { get; set; }
    public decimal HourlyCost { get; set; }
    public bool IsDefault { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class Consumable : IActiveEntity
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Material { get; set; }
    public required string Color { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal Density { get; set; }
    public bool IsDefault { get; set; }
    public bool Active { get; set; } = true;
    // Kept for compatibility with older clients/backups. New code uses StockGrams.
    public int StockQuantity { get; set; }
    public decimal StockGrams { get; set; }
    public decimal LowStockGrams { get; set; } = 1000m;
}

public sealed class ExtraMaterial : IActiveEntity
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class Quote
{
    public long Id { get; set; }
    public required string OrderCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public required string Customer { get; set; }
    public required string ProjectName { get; set; }
    public required string PrinterName { get; set; }
    public decimal PrintHours { get; set; }
    public int Quantity { get; set; }
    public decimal AdditionalManualCost { get; set; }
    public decimal ProfitMultiplier { get; set; }
    public string Notes { get; set; } = string.Empty;
    public decimal TotalWeight { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal ElectricityCost { get; set; }
    public decimal MachineCost { get; set; }
    public decimal MaintenanceCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal FunctionalSurcharge { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal RecommendedPrice { get; set; }
    public List<QuoteConsumable> Consumables { get; set; } = [];
    public List<QuoteMaterial> Materials { get; set; } = [];
    public Sale? Sale { get; set; }
}

public sealed class QuoteConsumable
{
    public long Id { get; set; }
    public long QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public long LegacyConsumableId { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Material { get; set; }
    public required string Color { get; set; }
    public decimal Grams { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal Density { get; set; }
    public decimal LineCost { get; set; }
}

public sealed class QuoteMaterial
{
    public long Id { get; set; }
    public long QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public long LegacyMaterialId { get; set; }
    public required string Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineCost { get; set; }
}

public sealed class Sale
{
    public long Id { get; set; }
    public long QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public DateTime SoldAtUtc { get; set; }
    public decimal SaleAmount { get; set; }
    public List<SaleConsumable> Consumables { get; set; } = [];
}

public sealed class SaleConsumable
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public long ConsumableId { get; set; }
    public Consumable Consumable { get; set; } = null!;
    public decimal Grams { get; set; }
}

public sealed class BusinessSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
