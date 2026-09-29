using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Netgum.Application.Abstractions;
using Netgum.Domain.Creators;
using Netgum.Domain.Orders;
using Netgum.Domain.Products;
using Netgum.Domain.ValueObjects;

namespace Netgum.Infrastructure.Persistence;

public sealed class NetgumDbContext : DbContext, IUnitOfWork
{
    public NetgumDbContext(DbContextOptions<NetgumDbContext> options)
        : base(options)
    {
    }

    public DbSet<Creator> Creators => Set<Creator>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CreatorConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductAssetConfiguration());
        modelBuilder.ApplyConfiguration(new ProductVariantConfiguration());
        modelBuilder.ApplyConfiguration(new CouponConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.Entity is not null))
        {
            if (entry.Entity is Netgum.Domain.Abstractions.Entity entity)
            {
                entity.ClearDomainEvents();
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);
}

internal sealed class CreatorConfiguration : IEntityTypeConfiguration<Creator>
{
    public void Configure(EntityTypeBuilder<Creator> builder)
    {
        builder.ToTable("creators");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Bio).HasMaxLength(2000);
        builder.Property(c => c.AvatarUrl).HasMaxLength(2048);
        builder.Property(c => c.PayoutAccountId).HasMaxLength(160);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.Slug)
            .HasConversion(slug => slug.Value, value => Slug.Create(value))
            .HasMaxLength(80)
            .IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.SupportEmail)
            .HasConversion(email => email.Value, value => EmailAddress.Create(value))
            .HasMaxLength(254)
            .IsRequired();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CreatorId).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(8000).IsRequired();
        builder.Property(p => p.CoverImageUrl).HasMaxLength(2048);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.PublishedAt);
        builder.Property(p => p.Slug)
            .HasConversion(slug => slug.Value, value => Slug.Create(value))
            .HasMaxLength(80)
            .IsRequired();
        builder.HasIndex(p => new { p.CreatorId, p.Slug }).IsUnique();
        builder.OwnsOne(p => p.Price, ConfigureMoney("price"));
        builder.HasMany(p => p.Assets).WithOne().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Variants).WithOne().HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Coupons).WithOne().HasForeignKey(c => c.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Assets).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Coupons).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static Action<OwnedNavigationBuilder<Product, Money>> ConfigureMoney(string prefix) =>
        money =>
        {
            money.Property(m => m.Amount).HasColumnName($"{prefix}_amount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName($"{prefix}_currency").HasMaxLength(3).IsRequired();
        };
}

internal sealed class ProductAssetConfiguration : IEntityTypeConfiguration<ProductAsset>
{
    public void Configure(EntityTypeBuilder<ProductAsset> builder)
    {
        builder.ToTable("product_assets");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ProductId).IsRequired();
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(160).IsRequired();
        builder.Property(a => a.SizeBytes).IsRequired();
        builder.Property(a => a.UploadedAt).IsRequired();
    }
}

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.ProductId).IsRequired();
        builder.Property(v => v.Name).HasMaxLength(120).IsRequired();
        builder.Property(v => v.StockQuantity);
        builder.Property(v => v.IsActive).IsRequired();
        builder.OwnsOne(v => v.AdditionalPrice, money =>
        {
            money.Property(m => m.Amount).HasColumnName("additional_price_amount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("additional_price_currency").HasMaxLength(3).IsRequired();
        });
    }
}

internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ProductId).IsRequired();
        builder.Property(c => c.Code).HasMaxLength(64).IsRequired();
        builder.Property(c => c.PercentOff);
        builder.Property(c => c.MaxRedemptions);
        builder.Property(c => c.RedeemedCount).IsRequired();
        builder.Property(c => c.ExpiresAt);
        builder.Property(c => c.IsActive).IsRequired();
        builder.HasIndex(c => new { c.ProductId, c.Code }).IsUnique();
        builder.OwnsOne(c => c.AmountOff, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount_off_amount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("amount_off_currency").HasMaxLength(3);
        });
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.ProductId).IsRequired();
        builder.Property(o => o.CreatorId).IsRequired();
        builder.Property(o => o.VariantId);
        builder.Property(o => o.CustomerName).HasMaxLength(160).IsRequired();
        builder.Property(o => o.Currency).HasMaxLength(3).IsRequired();
        builder.Property(o => o.PaymentIntentId).HasMaxLength(160).IsRequired();
        builder.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(o => o.LicenseKey).HasMaxLength(64).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.PaidAt);
        builder.HasIndex(o => o.PaymentIntentId).IsUnique();
        builder.Property(o => o.CustomerEmail)
            .HasConversion(email => email.Value, value => EmailAddress.Create(value))
            .HasMaxLength(254)
            .IsRequired();
        builder.OwnsOne(o => o.Subtotal, ConfigureMoney<Order>("subtotal"));
        builder.OwnsOne(o => o.Discount, ConfigureMoney<Order>("discount"));
        builder.OwnsOne(o => o.PlatformFee, ConfigureMoney<Order>("platform_fee"));
        builder.OwnsOne(o => o.Total, ConfigureMoney<Order>("total"));
    }

    private static Action<OwnedNavigationBuilder<TEntity, Money>> ConfigureMoney<TEntity>(string prefix)
        where TEntity : class =>
        money =>
        {
            money.Property(m => m.Amount).HasColumnName($"{prefix}_amount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName($"{prefix}_currency").HasMaxLength(3).IsRequired();
        };
}
