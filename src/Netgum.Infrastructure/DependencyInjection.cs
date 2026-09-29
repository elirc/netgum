using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Netgum.Application.Abstractions;
using Netgum.Infrastructure.Email;
using Netgum.Infrastructure.Payments;
using Netgum.Infrastructure.Persistence;

namespace Netgum.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Netgum")
            ?? throw new InvalidOperationException("Connection string 'Netgum' is missing.");

        services.AddDbContext<NetgumDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddHealthChecks().AddDbContextCheck<NetgumDbContext>("database");

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NetgumDbContext>());
        services.AddScoped<ICreatorRepository, CreatorRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        services.AddSingleton<IEmailSender, ConsoleEmailSender>();

        return services;
    }
}
