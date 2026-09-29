using Netgum.Application.Catalog;

namespace Netgum.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", async (
            string? term,
            int? page,
            int? pageSize,
            ProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SearchPublishedAsync(
                new ProductSearchQuery(term, page ?? 1, pageSize ?? 20),
                cancellationToken).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("SearchProducts")
        .WithOpenApi();

        group.MapPost("/", async (
            CreateProductRequest request,
            ProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var product = await service.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/api/products/{product.Id}", product);
        })
        .WithName("CreateProduct")
        .WithOpenApi();

        group.MapPut("/{productId:guid}", async (
            Guid productId,
            UpdateProductRequest request,
            ProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var product = await service.UpdateAsync(productId, request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(product);
        })
        .WithName("UpdateProduct")
        .WithOpenApi();

        group.MapPost("/{productId:guid}/coupons", async (
            Guid productId,
            AddCouponRequest request,
            ProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var product = await service.AddCouponAsync(productId, request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(product);
        })
        .WithName("AddCoupon")
        .WithOpenApi();

        group.MapPost("/{productId:guid}/publish", async (
            Guid productId,
            ProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var product = await service.PublishAsync(productId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(product);
        })
        .WithName("PublishProduct")
        .WithOpenApi();

        group.MapPost("/{productId:guid}/archive", async (
            Guid productId,
            ProductCatalogService service,
            CancellationToken cancellationToken) =>
        {
            var product = await service.ArchiveAsync(productId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(product);
        })
        .WithName("ArchiveProduct")
        .WithOpenApi();

        return app;
    }
}
