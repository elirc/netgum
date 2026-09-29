using Netgum.Application.Catalog;
using Netgum.Application.Creators;
using Netgum.Domain.Creators;
using Netgum.Domain.Orders;
using Netgum.Domain.Products;
using Netgum.Domain.ValueObjects;

namespace Netgum.Application.Abstractions;

public interface ICreatorRepository
{
    Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(Slug slug, CancellationToken cancellationToken);

    void Add(Creator creator);
}

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Product?> GetPublishedBySlugAsync(string creatorSlug, string productSlug, CancellationToken cancellationToken);

    Task<PagedResult<ProductSummary>> SearchPublishedAsync(ProductSearchQuery query, CancellationToken cancellationToken);

    Task<bool> SlugExistsForCreatorAsync(Guid creatorId, Slug slug, CancellationToken cancellationToken);

    void Add(Product product);
}

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Order?> GetByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken);

    void Add(Order order);
}
