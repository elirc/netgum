using Netgum.Application.Checkout;

namespace Netgum.Api.Endpoints;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/checkout").WithTags("Checkout");

        group.MapPost("/", async (
            CreateCheckoutRequest request,
            CheckoutService service,
            CancellationToken cancellationToken) =>
        {
            var checkout = await service.CreateSessionAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/api/orders/{checkout.OrderId}", checkout);
        })
        .WithName("CreateCheckout")
        .WithOpenApi();

        group.MapPost("/complete", async (
            CompleteOrderRequest request,
            CheckoutService service,
            CancellationToken cancellationToken) =>
        {
            var receipt = await service.CompleteOrderAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(receipt);
        })
        .WithName("CompleteCheckout")
        .WithOpenApi();

        return app;
    }
}
