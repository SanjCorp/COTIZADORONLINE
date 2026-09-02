namespace SanjCorp3D.Api.Contracts;

public sealed record BackupDocument(
    int Version, DateTime CreatedAtUtc, List<BackupPrinter> Printers, List<BackupConsumable> Consumables,
    List<BackupMaterial> Materials, List<BackupQuote> Quotes, Dictionary<string, string> Settings);

public sealed record BackupPrinter(string Name, decimal BuildX, decimal BuildY, decimal BuildZ, decimal Nozzle, decimal Speed, decimal PowerWatts, decimal HourlyCost, bool IsDefault, bool Active);
public sealed record BackupConsumable(string Name, string Category, string Material, string Color, decimal PricePerUnit, decimal Density, bool IsDefault, bool Active, int StockQuantity);
public sealed record BackupMaterial(string Name, string Category, string Unit, decimal UnitPrice, bool Active);
public sealed record BackupQuote(
    string OrderCode, DateTime CreatedAtUtc, string Customer, string ProjectName, string PrinterName, decimal PrintHours,
    int Quantity, decimal AdditionalManualCost, decimal ProfitMultiplier, string Notes, decimal TotalWeight,
    decimal MaterialCost, decimal ElectricityCost, decimal MachineCost, decimal MaintenanceCost, decimal LaborCost,
    decimal AdditionalCost, decimal FunctionalSurcharge, decimal Subtotal, decimal ProfitAmount, decimal TaxAmount,
    decimal RecommendedPrice, List<BackupQuoteConsumable> Consumables, List<BackupQuoteMaterial> Materials, BackupSale? Sale);
public sealed record BackupQuoteConsumable(long LegacyConsumableId, string Name, string Category, string Material, string Color, decimal Grams, decimal PricePerUnit, decimal Density, decimal LineCost);
public sealed record BackupQuoteMaterial(long LegacyMaterialId, string Name, decimal Quantity, decimal UnitPrice, decimal LineCost);
public sealed record BackupSale(DateTime SoldAtUtc, decimal SaleAmount);
public sealed record RestoreBackupRequest(string Confirmation, BackupDocument Backup);
