using Netgum.Application.Abstractions;
using Netgum.Domain.Orders;

namespace Netgum.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapGet("/{orderId:guid}/download", async (
            Guid orderId,
            IOrderRepository orders,
            CancellationToken cancellationToken) =>
        {
            var order = await orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (order is null)
            {
                return Results.NotFound();
            }

            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                return Results.Problem("Order has not been paid.", statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(new
            {
                order.Id,
                DownloadUrl = $"https://files.example.test/downloads/{order.ProductId}?license={order.LicenseKey}",
                order.LicenseKey
            });
        })
        .WithName("DownloadOrder")
        .WithOpenApi();

        return app;
    }
}
