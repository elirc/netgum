using Microsoft.EntityFrameworkCore;

using Netgum.Application.Abstractions;
using Netgum.Application.Catalog;
using Netgum.Domain.Creators;
using Netgum.Domain.Orders;
using Netgum.Domain.Products;
using Netgum.Domain.ValueObjects;

namespace Netgum.Infrastructure.Persistence;

internal sealed class CreatorRepository : ICreatorRepository
{
    private readonly NetgumDbContext _dbContext;

    public CreatorRepository(NetgumDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Creators.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(Slug slug, CancellationToken cancellationToken) =>
        _dbContext.Creators.AnyAsync(c => c.Slug == slug, cancellationToken);

    public void Add(Creator creator) => _dbContext.Creators.Add(creator);
}

internal sealed class ProductRepository : IProductRepository
{
    private readonly NetgumDbContext _dbContext;

    public ProductRepository(NetgumDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ProductAggregate().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Product?> GetPublishedBySlugAsync(string creatorSlug, string productSlug, CancellationToken cancellationToken)
    {
        var creator = await _dbContext.Creators
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Slug == Slug.Create(creatorSlug), cancellationToken)
            .ConfigureAwait(false);

        if (creator is null)
        {
            return null;
        }

        return await ProductAggregate()
            .SingleOrDefaultAsync(
                p => p.CreatorId == creator.Id && p.Slug == Slug.Create(productSlug) && p.Status == ProductStatus.Published,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PagedResult<ProductSummary>> SearchPublishedAsync(ProductSearchQuery query, CancellationToken cancellationToken)
    {
        var products =
            from product in _dbContext.Products.AsNoTracking()
            join creator in _dbContext.Creators.AsNoTracking() on product.CreatorId equals creator.Id
            where product.Status == ProductStatus.Published
            select new { product, creator };

        if (!string.IsNullOrWhiteSpace(query.Term))
        {
            products = products.Where(row =>
                row.product.Name.Contains(query.Term) ||
                row.product.Description.Contains(query.Term) ||
                row.creator.DisplayName.Contains(query.Term));
        }

        var totalCount = await products.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await products
            .OrderBy(row => row.product.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new ProductSummary(
                row.product.Id,
                row.creator.Slug.Value,
                row.product.Slug.Value,
                row.product.Name,
                row.product.Description,
                row.product.Price.Amount,
                row.product.Price.Currency,
                row.product.Status,
                row.product.CoverImageUrl))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ProductSummary>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<bool> SlugExistsForCreatorAsync(Guid creatorId, Slug slug, CancellationToken cancellationToken) =>
        _dbContext.Products.AnyAsync(p => p.CreatorId == creatorId && p.Slug == slug, cancellationToken);

    public void Add(Product product) => _dbContext.Products.Add(product);

    private IQueryable<Product> ProductAggregate() =>
        _dbContext.Products
            .Include(p => p.Assets)
            .Include(p => p.Variants)
            .Include(p => p.Coupons);
}

internal sealed class OrderRepository : IOrderRepository
{
    private readonly NetgumDbContext _dbContext;

    public OrderRepository(NetgumDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Orders.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Order?> GetByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken) =>
        _dbContext.Orders.SingleOrDefaultAsync(o => o.PaymentIntentId == paymentIntentId, cancellationToken);

    public void Add(Order order) => _dbContext.Orders.Add(order);
}
