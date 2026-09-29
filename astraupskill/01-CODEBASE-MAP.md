# 1. Codebase map

The Netgum request enters through `src/Netgum.Api/Endpoints/CheckoutEndpoints.cs`. Its POST endpoint binds `CreateCheckoutRequest` and resolves `CheckoutService` from dependency injection. The completion endpoint binds `CompleteOrderRequest` and calls the same service. `Program.cs` registers the EF `NetgumDbContext`, repositories, `SystemClock`, `FakePaymentGateway`, and `ConsoleEmailSender`. The fake components make local tests finite and make external-call boundaries visible.

The Application layer contains `CheckoutService`, request/response records, and abstractions. `IProductRepository.GetPublishedBySlugAsync` loads a product aggregate with assets, variants, and coupons. `IOrderRepository` adds an order and retrieves it by payment intent. `IUnitOfWork.SaveChangesAsync` commits the EF change tracker. `IPaymentGateway` creates an intent and reads its status. `IEmailSender` sends the purchase receipt.

The Domain layer owns preconditions. `Product` checks publication in `CalculateSubtotal`, resolves an optional variant, finds a coupon, and reserves inventory. `ProductVariant.ReserveOne` checks active state and stock, then decrements. `Coupon.CalculateDiscount` and `MarkRedeemed` check expiry, active state, and maximum redemptions. `Order.CreatePending` validates IDs, customer name/email, payment intent, and money totals. `Order.MarkPaid` is already idempotent for a paid order; `MarkPaymentFailed` transitions only a pending order.

The original checkout order was unsafe: it calculated values, called the payment gateway, reserved inventory, redeemed a coupon, and only then constructed the order. Customer email/name and variant inventory could fail after payment intent creation or tracked mutation. The repaired path adds small domain preflight methods and constructs the order before applying reservations.

`tests/Netgum.Tests/Application/CheckoutServiceTests.cs` uses EF InMemory and existing production repositories. Its new counting doubles implement the real `IPaymentGateway`, `IEmailSender`, and `IUnitOfWork` interfaces. A fixed clock supplies 2026-04-01 12:00 UTC. Tests add a published product with an asset and optional variant/coupon, then inspect tracked `StockQuantity`, `RedeemedCount`, order count, and call counters.

The product mutation endpoints have no seller authentication, and order download checks only a paid order ID. Those are existing demo limitations. This bounded pass does not invent creator ownership or real payment verification; it focuses on checkout ordering and terminal completion semantics.
