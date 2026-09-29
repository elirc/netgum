using Netgum.Domain.Abstractions;
using Netgum.Domain.ValueObjects;

namespace Netgum.Domain.Products;

public sealed class Product : Entity<Guid>
{
    private readonly List<ProductAsset> _assets = [];
    private readonly List<ProductVariant> _variants = [];
    private readonly List<Coupon> _coupons = [];

    private Product()
        : base(Guid.Empty)
    {
        Name = string.Empty;
        Slug = Slug.Create("temporary-product");
        Description = string.Empty;
        Price = Money.ZeroUsd;
    }

    private Product(Guid id, Guid creatorId, string name, Slug slug, string description, Money price)
        : base(id)
    {
        CreatorId = creatorId;
        Name = name;
        Slug = slug;
        Description = description;
        Price = price;
        Status = ProductStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CreatorId { get; private set; }

    public string Name { get; private set; }

    public Slug Slug { get; private set; }

    public string Description { get; private set; }

    public Money Price { get; private set; }

    public ProductStatus Status { get; private set; }

    public string? CoverImageUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyList<ProductAsset> Assets => _assets.AsReadOnly();

    public IReadOnlyList<ProductVariant> Variants => _variants.AsReadOnly();

    public IReadOnlyList<Coupon> Coupons => _coupons.AsReadOnly();

    public static Product Create(Guid creatorId, string name, string slug, string description, Money price)
    {
        if (creatorId == Guid.Empty)
        {
            throw new DomainException("Creator id is required.");
        }

        ValidateProductCopy(name, description);

        return new Product(Guid.NewGuid(), creatorId, name.Trim(), Slug.Create(slug), description.Trim(), price);
    }

    public void UpdateListing(string name, string description, string? coverImageUrl)
    {
        ValidateProductCopy(name, description);
        Name = name.Trim();
        Description = description.Trim();
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim();
    }

    public void ChangePrice(Money price)
    {
        if (Status == ProductStatus.Archived)
        {
            throw new DomainException("Cannot change price for an archived product.");
        }

        Price = price;
    }

    public ProductAsset AddAsset(string fileName, string storageKey, string contentType, long sizeBytes)
    {
        var asset = new ProductAsset(Guid.NewGuid(), fileName, storageKey, contentType, sizeBytes);
        _assets.Add(asset);
        return asset;
    }

    public ProductVariant AddVariant(string name, Money additionalPrice, int? stockQuantity)
    {
        var variant = new ProductVariant(Guid.NewGuid(), name, additionalPrice, stockQuantity);
        _variants.Add(variant);
        return variant;
    }

    public Coupon AddCoupon(string code, int? percentOff, Money? amountOff, int? maxRedemptions, DateTimeOffset? expiresAt)
    {
        if (_coupons.Any(c => c.Code == code.Trim().ToUpperInvariant()))
        {
            throw new DomainException("Coupon code already exists for this product.");
        }

        var coupon = new Coupon(Guid.NewGuid(), code, percentOff, amountOff, maxRedemptions, expiresAt);
        _coupons.Add(coupon);
        return coupon;
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
        {
            throw new DomainException("Archived products cannot be published.");
        }

        if (_assets.Count == 0)
        {
            throw new DomainException("Product must have at least one downloadable asset before publishing.");
        }

        Status = ProductStatus.Published;
        PublishedAt ??= now;
    }

    public void Archive() => Status = ProductStatus.Archived;

    public Money CalculateSubtotal(Guid? variantId)
    {
        if (Status != ProductStatus.Published)
        {
            throw new DomainException("Only published products can be purchased.");
        }

        var variant = variantId is null ? null : _variants.SingleOrDefault(v => v.Id == variantId.Value);
        if (variantId is not null && variant is null)
        {
            throw new DomainException("Selected variant does not exist.");
        }

        return variant is null ? Price : Price.Add(variant.AdditionalPrice);
    }

    public void ValidatePurchase(Guid? variantId)
    {
        if (Status != ProductStatus.Published)
        {
            throw new DomainException("Only published products can be purchased.");
        }

        if (variantId is null)
        {
            return;
        }

        var variant = _variants.SingleOrDefault(v => v.Id == variantId.Value)
            ?? throw new DomainException("Selected variant does not exist.");
        variant.ValidateReservation();
    }

    public void ReserveInventory(Guid? variantId)
    {
        if (variantId is null)
        {
            return;
        }

        _variants.Single(v => v.Id == variantId.Value).ReserveOne();
    }

    public Coupon? FindCoupon(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return _coupons.SingleOrDefault(c => c.Code == code.Trim().ToUpperInvariant());
    }

    private static void ValidateProductCopy(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name is required.");
        }

        if (name.Trim().Length > 120)
        {
            throw new DomainException("Product name cannot exceed 120 characters.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Product description is required.");
        }
    }
}
