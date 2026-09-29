using Microsoft.Extensions.DependencyInjection;

using Netgum.Application.Catalog;
using Netgum.Application.Checkout;
using Netgum.Application.Creators;

namespace Netgum.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreatorService>();
        services.AddScoped<ProductCatalogService>();
        services.AddScoped<CheckoutService>();

        return services;
    }
}
