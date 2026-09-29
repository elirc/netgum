using Netgum.Domain.Products;

namespace Netgum.Application.Catalog;

public sealed record CreateProductRequest(
    Guid CreatorId,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    string Currency,
    string FileName,
    string StorageKey,
    string ContentType,
    long SizeBytes);

public sealed record UpdateProductRequest(string Name, string Description, string? CoverImageUrl);

public sealed record AddCouponRequest(
    string Code,
    int? PercentOff,
    decimal? AmountOff,
    int? MaxRedemptions,
    DateTimeOffset? ExpiresAt);

public sealed record ProductSearchQuery(string? Term, int Page = 1, int PageSize = 20);

public sealed record ProductSummary(
    Guid Id,
    string CreatorSlug,
    string ProductSlug,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    ProductStatus Status,
    string? CoverImageUrl);

public sealed record ProductResponse(
    Guid Id,
    Guid CreatorId,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    string Currency,
    ProductStatus Status,
    string? CoverImageUrl,
    IReadOnlyList<ProductAssetResponse> Assets,
    IReadOnlyList<CouponResponse> Coupons);

public sealed record ProductAssetResponse(Guid Id, string FileName, string ContentType, long SizeBytes);

public sealed record CouponResponse(
    Guid Id,
    string Code,
    int? PercentOff,
    decimal? AmountOff,
    int RedeemedCount,
    int? MaxRedemptions,
    DateTimeOffset? ExpiresAt,
    bool IsActive);
