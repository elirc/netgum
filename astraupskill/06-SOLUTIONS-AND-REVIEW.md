# 6. Solutions and review

The examples are complete test methods or labeled production excerpts. Place the methods in `Netgum.Tests.Application.CheckoutServiceTests`; retain the existing usings and helper types. The checked-in file is the source of truth for exact constructor signatures.

## Solutions 1–4: reject before side effects

The invalid customer method is complete in the delivered test:

```csharp
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
        "ada-goods", "indie-saas-playbook", product.Variants.Single().Id,
        null, email, name), CancellationToken.None);
    await act.Should().ThrowAsync<DomainException>();
    product.Variants.Single().StockQuantity.Should().Be(2);
    payments.CreateCalls.Should().Be(0);
    (await dbContext.Orders.CountAsync()).Should().Be(0);
}
```

The sold-out and inactive solutions use the same helper and `service.Invoking(...).Should().ThrowAsync<DomainException>()`, then assert CreateCalls zero and unchanged `StockQuantity`. `Product.ValidatePurchase` is the actual domain preflight; do not copy `StockQuantity <= 0` into a test-only helper. The expired coupon solution adds an old coupon and asserts `RedeemedCount` zero.

## Solution 5: valid session

The production ordering excerpt is:

```csharp
var now = _clock.UtcNow;
Order.ValidateCustomer(request.CustomerEmail, request.CustomerName);
product.ValidatePurchase(request.VariantId);
var subtotal = product.CalculateSubtotal(request.VariantId);
var discount = coupon?.CalculateDiscount(subtotal, now) ?? Money.Create(0, subtotal.Currency);
var paymentIntent = await _payments.CreatePaymentIntentAsync(...);
product.ValidatePurchase(request.VariantId);
var order = Order.CreatePending(..., paymentIntent.Id, now);
product.ReserveInventory(request.VariantId);
coupon?.MarkRedeemed(now);
```

The ellipses identify an excerpt, not a copy-paste method. The actual file has the full request and money arguments. The test’s positive assertions must include a nonempty payment ID, expected total, stock one-to-zero, redemption zero-to-one, and pending order.

## Solutions 6–7: terminal completion

`CompleteOrderAsync` loads the product name, then returns immediately when `order.PaymentStatus` is Paid or Failed. Pending orders query the gateway. The success test checks `payments.StatusCalls == 1`, `email.Calls == 1`, and `unitOfWork.SaveCalls == 2` after two completion calls. The failure test checks status calls one, email zero, and saves two. The second call does not invoke `MarkPaid` or `MarkPaymentFailed`; terminal state is the persisted guard.

## Solution 8: ledger and limitation

Invalid customer: zero provider, zero inventory/coupon/order mutation. Sold-out/inactive: zero provider, stock unchanged. Expired coupon: zero provider, redemption and stock unchanged. Provider Failed: one status query, one order save, no email. EF save failure after provider and tracked mutation remains a residual consistency risk because no distributed transaction or compensation exists.

## Solution 9: proposed extension

A production command would authenticate creator and customer, validate an idempotency key, reserve stock with a conditional database update, create or reuse a provider intent, and persist an order plus outbox event in a transaction. A webhook would verify provider signatures before completing. The current app does none of this; its demo creator mutation routes lack authentication and download checks only paid order ID. Keep those follow-up risks in the review.

## Complete executable fixture

The following is the complete current `tests/Netgum.Tests/Application/CheckoutServiceTests.cs`, including imports, namespace, fixture graph and helper doubles. These tests already exist: modify the original methods when practicing instead of adding duplicate method names. Sold-out and inactive variants are separate cases so one guard cannot hide a failure in the other. The original no-coupon case provides a positive total assertion; the valid coupon case covers reservation, redemption and sequential repeated completion.

```csharp
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
```

## Complete repeated-completion assertions

When diagnosing duplicate work, assert the state and every boundary counter together. In `Completion_IsTerminalAndSendsReceiptOnlyOnce`, the first call observes `Succeeded`, marks the order paid, saves, and sends the receipt. The second call reads the same order and returns before `GetPaymentIntentStatusAsync`. Thus `StatusCalls` is one, `email.Calls` is one, and `SaveCalls` is two: the session save plus the first completion save. The product and coupon scalar assertions prove that the second call did not reserve or redeem again. For the failed case, `MarkPaymentFailed` is reached only once, and a second call returns the failed receipt without a third save.

If a future repository implementation returns a detached order from a new context, the same contract still requires the terminal status check before provider work. The current test intentionally uses the actual EF repository and InMemory provider, so it catches mapping and state-transition mistakes. It cannot prove two simultaneous requests are serialized, and it cannot prove that a provider intent is cancelled after a database outage. Those claims belong in an integration design with a transaction, conditional stock update, provider idempotency key, and outbox.

## What a receipt retry cannot guarantee yet

The service saves Paid before sending the receipt. If the sender throws, the order is already paid; a later call returns early and does not retry the email. Two callers that both loaded Pending can also reach provider/save/email work concurrently because no database version predicate coordinates them. The eleven tests cover sequential service calls, not either failure window. A future outbox should persist a unique order-paid delivery record with the state change and let a retrying worker record delivery attempts; provider idempotency and database concurrency need separate tests. Do not move the email before SaveChanges as a shortcut: that creates a receipt for an order whose paid state may never commit.

Product names are read from the current product on each successful receipt response; they are not frozen on the order. Sequential equivalence in the test assumes the product name has not changed. Coupon eligibility uses the captured command time both before and after the provider await, and the second preflight inspects the same tracked object. It neither refreshes another DbContext's writes nor establishes wall-clock expiry at payment-provider completion.
