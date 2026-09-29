using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using Netgum.Application.Abstractions;
using Netgum.Application.Checkout;
using Netgum.Domain.Abstractions;
using Netgum.Domain.Creators;
using Netgum.Domain.Products;
using Netgum.Domain.ValueObjects;
using Netgum.Infrastructure;
using Netgum.Infrastructure.Email;
using Netgum.Infrastructure.Payments;
using Netgum.Infrastructure.Persistence;
using Netgum.Domain.Orders;

namespace Netgum.Tests.Application;

public sealed class CheckoutServiceTests
{
    [Fact]
    public async Task Checkout_session_creates_pending_order_and_completion_fulfills_it()
    {
        await using var dbContext = CreateDbContext();
        var creator = Creator.Register("Ada Goods", "ada-goods", "ada@example.com");
        var product = Product.Create(
            creator.Id,
            "Indie SaaS Playbook",
            "indie-saas-playbook",
            "A practical guide for solo founders.",
            Money.Create(49m, "USD"));
        product.AddAsset("playbook.pdf", "products/playbook.pdf", "application/pdf", 42_000);
        product.Publish(DateTimeOffset.UtcNow);

        dbContext.Creators.Add(creator);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var checkout = await service.CreateSessionAsync(
            new CreateCheckoutRequest(
                "ada-goods",
                "indie-saas-playbook",
                null,
                null,
                "reader@example.com",
                "Reader Person"),
            CancellationToken.None);

        checkout.Total.Should().Be(51.45m);
        checkout.PaymentIntentId.Should().StartWith("fake_pi_");

        var receipt = await service.CompleteOrderAsync(
            new CompleteOrderRequest(checkout.PaymentIntentId),
            CancellationToken.None);

        receipt.PaymentStatus.Should().Be(Netgum.Domain.Orders.PaymentStatus.Paid);
        receipt.OrderStatus.Should().Be(Netgum.Domain.Orders.OrderStatus.Fulfilled);
        receipt.LicenseKey.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("invalid-email", "Reader Person")]
    [InlineData("reader@example.com", "")]
    public async Task InvalidCustomerInput_DoesNotCallGatewayOrReserve(string email, string name)
    {
        await using var dbContext = CreateDbContext();
        var product = AddPublishedProduct(dbContext, stock: 2);
        var payments = new CountingPaymentGateway();
        var service = CreateService(dbContext, payments, new CountingEmailSender());

        var act = () => service.CreateSessionAsync(new CreateCheckoutRequest(
            "ada-goods", "indie-saas-playbook", product.Variants.Single().Id, null, email, name), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        product.Variants.Single().StockQuantity.Should().Be(2);
        payments.CreateCalls.Should().Be(0);
        (await dbContext.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SoldOutVariant_DoesNotCallGatewayOrChangeTrackedInventory()
    {
        await using var dbContext = CreateDbContext();
        var product = AddPublishedProduct(dbContext, stock: 0);
        var payments = new CountingPaymentGateway();
        var service = CreateService(dbContext, payments, new CountingEmailSender());

        await service.Invoking(s => s.CreateSessionAsync(new CreateCheckoutRequest(
            "ada-goods", "indie-saas-playbook", product.Variants.Single().Id, null,
            "reader@example.com", "Reader Person"), CancellationToken.None))
            .Should().ThrowAsync<DomainException>();

        product.Variants.Single().StockQuantity.Should().Be(0);
        payments.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task InactiveVariant_DoesNotCallGatewayOrChangeTrackedInventory()
    {
        await using var dbContext = CreateDbContext();
        var product = AddPublishedProduct(dbContext, stock: 2);
        var variant = product.Variants.Single();
        variant.Deactivate();
        var payments = new CountingPaymentGateway();
        var service = CreateService(dbContext, payments, new CountingEmailSender());

        await service.Invoking(s => s.CreateSessionAsync(new CreateCheckoutRequest(
            "ada-goods", "indie-saas-playbook", variant.Id, null,
            "reader@example.com", "Reader Person"), CancellationToken.None))
            .Should().ThrowAsync<DomainException>();

        variant.StockQuantity.Should().Be(2);
        payments.CreateCalls.Should().Be(0);
        (await dbContext.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExpiredCoupon_DoesNotCallGatewayOrRedeemCoupon()
    {
        await using var dbContext = CreateDbContext();
        var product = AddPublishedProduct(dbContext, stock: 1);
        var coupon = product.AddCoupon("OLD10", 10, null, 5, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var payments = new CountingPaymentGateway();
        var service = CreateService(dbContext, payments, new CountingEmailSender());

        await service.Invoking(s => s.CreateSessionAsync(new CreateCheckoutRequest(
            "ada-goods", "indie-saas-playbook", product.Variants.Single().Id, "old10",
            "reader@example.com", "Reader Person"), CancellationToken.None))
            .Should().ThrowAsync<DomainException>();

        coupon.RedeemedCount.Should().Be(0);
        product.Variants.Single().StockQuantity.Should().Be(1);
        payments.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task Completion_IsTerminalAndSendsReceiptOnlyOnce()
    {
        await using var dbContext = CreateDbContext();
        var product = AddPublishedProduct(dbContext, stock: 1, withValidCoupon: true);
        var coupon = product.Coupons.Single();
        var payments = new CountingPaymentGateway { Status = PaymentIntentStatus.Succeeded };
        var email = new CountingEmailSender();
        var unitOfWork = new CountingUnitOfWork(dbContext);
        var service = CreateService(dbContext, payments, email, unitOfWork);

        var checkout = await service.CreateSessionAsync(new CreateCheckoutRequest(
            "ada-goods", "indie-saas-playbook", product.Variants.Single().Id, "save10",
            "reader@example.com", "Reader Person"), CancellationToken.None);
        var first = await service.CompleteOrderAsync(new CompleteOrderRequest(checkout.PaymentIntentId), CancellationToken.None);
        var second = await service.CompleteOrderAsync(new CompleteOrderRequest(checkout.PaymentIntentId), CancellationToken.None);

        first.PaymentStatus.Should().Be(PaymentStatus.Paid);
        second.Should().BeEquivalentTo(first);
        product.Variants.Single().StockQuantity.Should().Be(0);
        coupon.RedeemedCount.Should().Be(1);
        payments.CreateCalls.Should().Be(1);
        payments.StatusCalls.Should().Be(1);
        email.Calls.Should().Be(1);
        unitOfWork.SaveCalls.Should().Be(2);
    }

    [Fact]
    public async Task FailedCompletion_IsTerminalWithoutDuplicateProviderCall()
    {
        await using var dbContext = CreateDbContext();
        var product = AddPublishedProduct(dbContext, stock: null);
        var payments = new CountingPaymentGateway { Status = PaymentIntentStatus.Failed };
        var email = new CountingEmailSender();
        var unitOfWork = new CountingUnitOfWork(dbContext);
        var service = CreateService(dbContext, payments, email, unitOfWork);

        var checkout = await service.CreateSessionAsync(new CreateCheckoutRequest(
            "ada-goods", "indie-saas-playbook", null, null,
            "reader@example.com", "Reader Person"), CancellationToken.None);
        var first = await service.CompleteOrderAsync(new CompleteOrderRequest(checkout.PaymentIntentId), CancellationToken.None);
        var second = await service.CompleteOrderAsync(new CompleteOrderRequest(checkout.PaymentIntentId), CancellationToken.None);

        first.PaymentStatus.Should().Be(PaymentStatus.Failed);
        second.PaymentStatus.Should().Be(PaymentStatus.Failed);
        payments.StatusCalls.Should().Be(1);
        email.Calls.Should().Be(0);
        unitOfWork.SaveCalls.Should().Be(2);
    }

    private static NetgumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NetgumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new NetgumDbContext(options);
    }

    private static CheckoutService CreateService(NetgumDbContext dbContext)
    {
        IOrderRepository orders = new OrderRepository(dbContext);
        IProductRepository products = new ProductRepository(dbContext);
        IPaymentGateway payments = new FakePaymentGateway();
        IEmailSender email = new ConsoleEmailSender(NullLogger<ConsoleEmailSender>.Instance);

        return new CheckoutService(
            new SystemClock(),
            email,
            orders,
            payments,
            products,
            dbContext);
    }

    private static CheckoutService CreateService(
        NetgumDbContext dbContext,
        CountingPaymentGateway payments,
        CountingEmailSender email,
        CountingUnitOfWork? unitOfWork = null) =>
        new(
            new FixedClock(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero)),
            email,
            new OrderRepository(dbContext),
            payments,
            new ProductRepository(dbContext),
            unitOfWork ?? new CountingUnitOfWork(dbContext));

    private static Product AddPublishedProduct(NetgumDbContext dbContext, int? stock, bool withValidCoupon = false)
    {
        var creator = Creator.Register("Ada Goods", "ada-goods", "ada@example.com");
        var product = Product.Create(creator.Id, "Indie SaaS Playbook", "indie-saas-playbook", "A practical guide for solo founders.", Money.Create(49m, "USD"));
        product.AddAsset("playbook.pdf", "products/playbook.pdf", "application/pdf", 42_000);
        product.AddVariant("Standard", Money.ZeroUsd, stock);
        if (withValidCoupon)
        {
            product.AddCoupon("SAVE10", 10, null, 5, new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero));
        }
        product.Publish(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        dbContext.Creators.Add(creator);
        dbContext.Products.Add(product);
        dbContext.SaveChanges();
        return product;
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock;

    private sealed class CountingPaymentGateway : IPaymentGateway
    {
        public int CreateCalls { get; private set; }
        public int StatusCalls { get; private set; }
        public PaymentIntentStatus Status { get; init; } = PaymentIntentStatus.Processing;

        public Task<PaymentIntent> CreatePaymentIntentAsync(PaymentIntentRequest request, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(new PaymentIntent("pi_test", new Uri("https://pay.test/checkout"), PaymentIntentStatus.Processing));
        }

        public Task<PaymentIntentStatus> GetPaymentIntentStatusAsync(string paymentIntentId, CancellationToken cancellationToken)
        {
            StatusCalls++;
            return Task.FromResult(Status);
        }
    }

    private sealed class CountingEmailSender : IEmailSender
    {
        public int Calls { get; private set; }
        public Task SendPurchaseReceiptAsync(PurchaseReceiptEmail email, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingUnitOfWork(NetgumDbContext dbContext) : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
