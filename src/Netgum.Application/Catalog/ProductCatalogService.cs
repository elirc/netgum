using Netgum.Application.Abstractions;
using Netgum.Domain.Abstractions;
using Netgum.Domain.Products;
using Netgum.Domain.ValueObjects;

namespace Netgum.Application.Catalog;

public sealed class ProductCatalogService
{
    private readonly IClock _clock;
    private readonly ICreatorRepository _creators;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public ProductCatalogService(
        IClock clock,
        ICreatorRepository creators,
        IProductRepository products,
        IUnitOfWork unitOfWork)
    {
        _clock = clock;
        _creators = creators;
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<ProductSummary>> SearchPublishedAsync(ProductSearchQuery query, CancellationToken cancellationToken) =>
        _products.SearchPublishedAsync(Normalize(query), cancellationToken);

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var creator = await _creators.GetByIdAsync(request.CreatorId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Creator was not found.");

        var slug = Slug.Create(request.Slug);
        if (await _products.SlugExistsForCreatorAsync(creator.Id, slug, cancellationToken).ConfigureAwait(false))
        {
            throw new DomainException("Product slug is already taken for this creator.");
        }

        var product = Product.Create(
            creator.Id,
            request.Name,
            slug.Value,
            request.Description,
            Money.Create(request.Price, request.Currency));

        product.AddAsset(request.FileName, request.StorageKey, request.ContentType, request.SizeBytes);

        _products.Add(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ProductMapper.ToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrow(productId, cancellationToken).ConfigureAwait(false);
        product.UpdateListing(request.Name, request.Description, request.CoverImageUrl);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProductMapper.ToResponse(product);
    }

    public async Task<ProductResponse> AddCouponAsync(Guid productId, AddCouponRequest request, CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrow(productId, cancellationToken).ConfigureAwait(false);
        var amountOff = request.AmountOff is null ? null : Money.Create(request.AmountOff.Value, product.Price.Currency);
        product.AddCoupon(request.Code, request.PercentOff, amountOff, request.MaxRedemptions, request.ExpiresAt);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProductMapper.ToResponse(product);
    }

    public async Task<ProductResponse> PublishAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrow(productId, cancellationToken).ConfigureAwait(false);
        product.Publish(_clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProductMapper.ToResponse(product);
    }

    public async Task<ProductResponse> ArchiveAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await GetProductOrThrow(productId, cancellationToken).ConfigureAwait(false);
        product.Archive();

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProductMapper.ToResponse(product);
    }

    private async Task<Product> GetProductOrThrow(Guid productId, CancellationToken cancellationToken) =>
        await _products.GetByIdAsync(productId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Product was not found.");

    private static ProductSearchQuery Normalize(ProductSearchQuery query) =>
        query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100),
            Term = string.IsNullOrWhiteSpace(query.Term) ? null : query.Term.Trim()
        };
}

internal static class ProductMapper
{
    public static ProductResponse ToResponse(Product product) =>
        new(
            product.Id,
            product.CreatorId,
            product.Name,
            product.Slug.Value,
            product.Description,
            product.Price.Amount,
            product.Price.Currency,
            product.Status,
            product.CoverImageUrl,
            product.Assets.Select(a => new ProductAssetResponse(a.Id, a.FileName, a.ContentType, a.SizeBytes)).ToArray(),
            product.Coupons.Select(c => new CouponResponse(
                c.Id,
                c.Code,
                c.PercentOff,
                c.AmountOff?.Amount,
                c.RedeemedCount,
                c.MaxRedemptions,
                c.ExpiresAt,
                c.IsActive)).ToArray());
}
