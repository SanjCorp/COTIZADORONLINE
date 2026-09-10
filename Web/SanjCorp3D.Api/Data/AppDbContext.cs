using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Models;

namespace SanjCorp3D.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<Consumable> Consumables => Set<Consumable>();
    public DbSet<ExtraMaterial> Materials => Set<ExtraMaterial>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteConsumable> QuoteConsumables => Set<QuoteConsumable>();
    public DbSet<QuoteMaterial> QuoteMaterials => Set<QuoteMaterial>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleConsumable> SaleConsumables => Set<SaleConsumable>();
    public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("sanjcorp");
        builder.Entity<Printer>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<Consumable>().HasIndex(x => new { x.Name, x.Material, x.Color }).IsUnique();
        builder.Entity<Consumable>().Property(x => x.StockGrams).HasDefaultValue(0m);
        builder.Entity<Consumable>().Property(x => x.LowStockGrams).HasDefaultValue(1000m);
        builder.Entity<ExtraMaterial>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<Quote>().HasIndex(x => x.OrderCode).IsUnique();
        builder.Entity<Quote>().HasIndex(x => x.CreatedAtUtc);
        builder.Entity<Quote>().HasIndex(x => x.Customer);
        builder.Entity<Sale>().HasIndex(x => x.QuoteId).IsUnique();
        builder.Entity<SaleConsumable>().HasIndex(x => x.SaleId);
        builder.Entity<SaleConsumable>().HasIndex(x => x.ConsumableId);
        builder.Entity<BusinessSetting>().HasKey(x => x.Key);
        builder.Entity<Quote>().HasMany(x => x.Consumables).WithOne(x => x.Quote).HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Quote>().HasMany(x => x.Materials).WithOne(x => x.Quote).HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Quote>().HasOne(x => x.Sale).WithOne(x => x.Quote).HasForeignKey<Sale>(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Sale>().HasMany(x => x.Consumables).WithOne(x => x.Sale).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<SaleConsumable>().HasOne(x => x.Consumable).WithMany().HasForeignKey(x => x.ConsumableId).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in builder.Model.GetEntityTypes().SelectMany(type => type.GetProperties()).Where(property => property.ClrType == typeof(decimal)))
        {
            property.SetPrecision(18);
            property.SetScale(4);
        }
    }
}
