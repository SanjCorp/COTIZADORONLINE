namespace SanjCorp3D.Api.Contracts;

public sealed record BusinessSettingsDto(
    string BusinessName, string CurrencyName, string CurrencySymbol,
    decimal ElectricityPerKwh, decimal MaintenancePerPrint,
    decimal DefaultProfitMultiplier, decimal TaxPercent,
    decimal RoundTo, int DecimalPlaces);

public sealed record ConsumableUsageRequest(long ConsumableId, decimal Grams);
public sealed record MaterialUsageRequest(long MaterialId, decimal Quantity);

public sealed record InventoryAlertDto(
    long Id, string Name, string Category, string Material, string Color,
    decimal StockGrams, decimal LowStockGrams, string Severity, string Message);

public sealed record QuoteRequest(
    string Customer, string ProjectName, long PrinterId, decimal PrintHours,
    int Quantity, decimal AdditionalManualCost, decimal ProfitMultiplier,
    string Notes, IReadOnlyList<ConsumableUsageRequest> Consumables,
    IReadOnlyList<MaterialUsageRequest> Materials, string? CustomerPhone = null, string? ProductName = null);

public sealed record QuoteCalculationDto(
    decimal TotalWeight, decimal MaterialCost, decimal ElectricityCost,
    decimal MaintenanceCost, decimal AdditionalCost, decimal Subtotal,
    decimal ProfitAmount, decimal TaxAmount, decimal RecommendedPrice);

public sealed record CreateUserRequest(string Username, string DisplayName, string? Email, string Password, string Role, string? ProfilePhotoUrl = null);
public sealed record UpdateUserRequest(string DisplayName, string? Email, string Role, bool Active, string? ProfilePhotoUrl = null);
public sealed record TenantDto(Guid Id, string Name, string Slug, string Kind, string? LogoUrl, bool Active, DateTime CreatedAtUtc, int UserCount);
public sealed record CreateMakerTenantRequest(string Name, string Slug, string? LogoUrl, string Username, string DisplayName, string? Email, string Password);
public sealed record UpdateTenantRequest(string Name, string? LogoUrl, bool Active);
public sealed record CreateMakerUserRequest(string Username, string DisplayName, string? Email, string Password);
public sealed record ProductCatalogDto(long Id, string Name, bool Active);
public sealed record CreateProductRequest(string Name);
public sealed record InventoryLossRequest(decimal Grams, string Reason);
