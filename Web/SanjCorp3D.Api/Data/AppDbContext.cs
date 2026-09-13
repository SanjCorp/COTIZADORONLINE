using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SanjCorp3D.Api.Identity;
using SanjCorp3D.Api.Models;
using SanjCorp3D.Api.Services;

namespace SanjCorp3D.Api.Data;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly TenantContext tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, TenantContext tenantContext)
        : base(options) => this.tenantContext = tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : this(options, new TenantContext()) { }

    public Guid? CurrentTenantId => tenantContext.CurrentTenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
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
        builder.Entity<Tenant>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<Printer>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.Entity<Printer>().HasIndex(x => x.CatalogId);
        builder.Entity<Consumable>().HasIndex(x => new { x.TenantId, x.Name, x.Material, x.Color }).IsUnique();
        builder.Entity<Consumable>().Property(x => x.StockGrams).HasDefaultValue(0m);
        builder.Entity<Consumable>().Property(x => x.LowStockGrams).HasDefaultValue(1000m);
        builder.Entity<ExtraMaterial>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.Entity<Quote>().HasIndex(x => new { x.TenantId, x.OrderCode }).IsUnique();
        builder.Entity<Quote>().HasIndex(x => x.CreatedAtUtc);
        builder.Entity<Quote>().HasIndex(x => x.Customer);
        builder.Entity<Sale>().HasIndex(x => new { x.TenantId, x.QuoteId }).IsUnique();
        builder.Entity<SaleConsumable>().HasIndex(x => x.SaleId);
        builder.Entity<SaleConsumable>().HasIndex(x => x.ConsumableId);
        builder.Entity<BusinessSetting>().HasKey(x => new { x.TenantId, x.Key });
        builder.Entity<QuoteConsumable>().HasKey(x => x.Id);
        builder.Entity<QuoteMaterial>().HasKey(x => x.Id);
        builder.Entity<SaleConsumable>().HasKey(x => x.Id);
        builder.Entity<QuoteConsumable>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);
        builder.Entity<QuoteMaterial>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);
        builder.Entity<SaleConsumable>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);
        builder.Entity<Quote>().HasMany(x => x.Consumables).WithOne(x => x.Quote).HasForeignKey(x => new { x.TenantId, x.QuoteId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Quote>().HasMany(x => x.Materials).WithOne(x => x.Quote).HasForeignKey(x => new { x.TenantId, x.QuoteId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Quote>().HasOne(x => x.Sale).WithOne(x => x.Quote).HasForeignKey<Sale>(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Sale>().HasMany(x => x.Consumables).WithOne(x => x.Sale).HasForeignKey(x => new { x.TenantId, x.SaleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<SaleConsumable>().HasOne(x => x.Consumable).WithMany().HasForeignKey(x => new { x.TenantId, x.ConsumableId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in builder.Model.GetEntityTypes().SelectMany(type => type.GetProperties()).Where(property => property.ClrType == typeof(decimal)))
        {
            property.SetPrecision(18);
            property.SetScale(4);
        }

        builder.Entity<Printer>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);
        builder.Entity<Consumable>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);
        builder.Entity<ExtraMaterial>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);
        builder.Entity<Quote>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);
        builder.Entity<Sale>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);
        builder.Entity<BusinessSetting>().HasQueryFilter(x => CurrentTenantId.HasValue && x.TenantId == CurrentTenantId);

        builder.Entity<Printer>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Consumable>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ExtraMaterial>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Quote>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Sale>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<BusinessSetting>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Quote>().HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Entity<Sale>().HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Entity<Consumable>().HasAlternateKey(x => new { x.TenantId, x.Id });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenantIds();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenantIds();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampTenantIds()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (!CurrentTenantId.HasValue)
                throw new InvalidOperationException("No se seleccionó un espacio de trabajo.");
            if (entry.State == EntityState.Added) entry.Entity.TenantId = CurrentTenantId.Value;
            else if ((entry.State == EntityState.Modified || entry.State == EntityState.Deleted) && entry.Entity.TenantId != CurrentTenantId.Value)
                throw new InvalidOperationException("No puedes modificar datos de otro espacio de trabajo.");
        }
    }
}
