# Astra upskill: safe Netgum checkout

This course follows one bounded checkout repair in Netgum, a .NET 8 digital-product application. The workflow creates a payment intent, reserves optional variant inventory, redeems an optional coupon, persists a pending order, and later completes that order from a payment-provider status. The improvement is to validate deterministic input before an external gateway call or tracked entity mutation, then make paid and failed completion terminal and idempotent.

Read chapters 01–07 in order, then `VERIFICATION.md`. Chapter 01 maps the application, domain, EF repository, fake gateway, console email, and API. Chapter 02 explains domain preflight, money, coupons, and terminal states. Chapter 03 works through the actual before/after lifecycle. Chapter 04 shows deterministic fake testing and failure diagnosis. Chapter 05 provides nine exercises plus two hands-on labs (break the ordering on purpose; red-first from the snapshots); chapter 06 gives matched solutions with complete fixtures and call-count assertions. Chapter 07 traces requests and failure ledgers.

The source change adds `Product.ValidatePurchase` and `ProductVariant.ValidateReservation`, plus `Order.ValidateCustomer`. `CheckoutService` captures one clock value, validates customer/product/coupon state, calls the gateway only after preflight, revalidates after the await, builds the pending order before reserving inventory, and then saves. `CompleteOrderAsync` returns existing paid or failed receipts before provider, save, or email work. The fake gateway and console email remain demo infrastructure.

Tests use EF Core InMemory with counting gateway, email, and unit-of-work doubles. They prove no external calls or tracked field changes on invalid requests and prove one-time stock redemption, provider status, receipt email, and save behavior on repeated completion. EF InMemory does not prove relational constraints, SQL transactions, distributed payment consistency, or cross-context stock concurrency.

Original changed source/test files are byte-exact snapshots under `snapshots/`. The README and limits keep the existing unauthenticated product mutation and ID-holder download capabilities explicit; seller identity and production payment security are follow-up work.


## Prerequisites and glossary

**Prerequisites:** C# classes and `async`/`await`; one HTTP endpoint written in any language; enums and interfaces. EF Core, DDD and xUnit are introduced here. The sibling `netzen` course is a natural predecessor for layered .NET vocabulary.

| term | meaning | where in this project |
| --- | --- | --- |
| aggregate | an object graph loaded, changed and saved as one unit | `Product` in `src/Netgum.Domain/Products/Product.cs`, `Order` in `src/Netgum.Domain/Orders/Order.cs` |
| preflight validation | a non-mutating check run before any gateway call or tracked mutation | `Product.ValidatePurchase` and `Order.ValidateCustomer` in `src/Netgum.Domain` |
| reservation check | the stock question asked without decrementing anything | `ProductVariant.ValidateReservation` in `src/Netgum.Domain/Products/ProductVariant.cs` |
| payment intent | the provider's record of a pending charge; creating one is a real side effect | `CreatePaymentIntentAsync` called from `src/Netgum.Application/Checkout/CheckoutService.cs` |
| captured clock value | one timestamp read at the top of the use case and reused | `var now = _clock.UtcNow;` in `CheckoutService.CreateSessionAsync` |
| tracked entity | an object EF Core watches, so touching a property marks it dirty | `NetgumDbContext` in `src/Netgum.Infrastructure/Persistence/NetgumDbContext.cs` |
| unit of work | the single call flushing all pending changes, wrapped so tests can count it | `IUnitOfWork` in `src/Netgum.Application/Abstractions/IUnitOfWork.cs` |
| terminal state | a payment state nothing moves out of | `Paid` and `Failed` in `src/Netgum.Domain/Orders/PaymentStatus.cs` |
| idempotent completion | settling twice returns the same answer and repeats no side effect | `CompleteOrderAsync` in `CheckoutService.cs` |
| receipt | the value returned once an order has settled | `OrderReceiptResponse` in `src/Netgum.Application/Checkout/CheckoutContracts.cs` |
| coupon redemption | the one-time consumption of a coupon, applied after preflight | `Coupon.MarkRedeemed` in `src/Netgum.Domain/Products/Coupon.cs` |
| counting double | a stand-in recording call counts, which is how a test proves no call happened | `CountingPaymentGateway` in `tests/Netgum.Tests/Application/CheckoutServiceTests.cs` |

## Before you start

**What you need installed.**

| Tool | Version used | Why |
|---|---|---|
| .NET SDK | 10.0.302 (pinned in `global.json` at the project root) | builds and runs everything |
| .NET 8 runtime *or* `DOTNET_ROLL_FORWARD=Major` | projects target `net8.0` | the test host needs a runtime for the target framework; with only .NET 10 installed, set `DOTNET_ROLL_FORWARD=Major` before `dotnet test` |
| An editor with C# support | any | VS Code + C# Dev Kit, Rider or Visual Studio |

No database server, Docker, Node or Python is needed: persistence in the tests is EF Core InMemory and the payment gateway and email sender are in-process fakes.

**Time estimate (about 6–8 hours total, comfortably three sittings).**

| Chapter | Estimate |
|---|---|
| 01 Codebase map | 30 min |
| 02 Concepts | 60 min |
| 03 Worked change | 60 min |
| 04 Testing and debugging | 45 min |
| 05 Practice (9 exercises + two labs) | 2–3 h |
| 06 Solutions and review | 60 min |
| 07 Trace lab | 45 min |
| VERIFICATION | 15 min |


## Open the code while studying

Follow the [map](01-CODEBASE-MAP.md), [concepts](02-CONCEPTS.md), [worked repair](03-WORKED-CHANGE.md), [tests](04-TESTING-AND-DEBUGGING.md), [practice](05-PRACTICE.md), [solutions](06-SOLUTIONS-AND-REVIEW.md), [trace](07-TRACE-LAB.md), and [verification](VERIFICATION.md). Compare the [actual checkout service](../src/Netgum.Application/Checkout/CheckoutService.cs), [order aggregate](../src/Netgum.Domain/Orders/Order.cs), and [executable regression fixture](../tests/Netgum.Tests/Application/CheckoutServiceTests.cs). A sequential repeat of an already-paid order avoids another email; this does not mean concurrent pending requests or a failed email send have durable delivery guarantees.
