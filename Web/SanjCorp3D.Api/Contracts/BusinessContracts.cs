namespace SanjCorp3D.Api.Contracts;

public sealed record BusinessSettingsDto(
    string BusinessName, string CurrencyName, string CurrencySymbol,
    decimal ElectricityPerKwh, decimal MaintenancePerPrint,
    decimal DefaultProfitMultiplier, decimal TaxPercent,
    decimal RoundTo, int DecimalPlaces);

public sealed record ConsumableUsageRequest(long ConsumableId, decimal Grams);
public sealed record MaterialUsageRequest(long MaterialId, decimal Quantity);

public sealed record QuoteRequest(
    string Customer, string ProjectName, long PrinterId, decimal PrintHours,
    int Quantity, decimal AdditionalManualCost, decimal ProfitMultiplier,
    string Notes, IReadOnlyList<ConsumableUsageRequest> Consumables,
    IReadOnlyList<MaterialUsageRequest> Materials);

public sealed record QuoteCalculationDto(
    decimal TotalWeight, decimal MaterialCost, decimal ElectricityCost,
    decimal MaintenanceCost, decimal AdditionalCost, decimal Subtotal,
    decimal ProfitAmount, decimal TaxAmount, decimal RecommendedPrice);

public sealed record CreateUserRequest(string Username, string DisplayName, string? Email, string Password, string Role);
public sealed record UpdateUserRequest(string DisplayName, string? Email, string Role, bool Active);
