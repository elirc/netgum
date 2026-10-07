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

`CompleteOrderAsync` returns immediately for a Failed order (`CheckoutService.cs:102-105`) without loading the product; for any other status it loads the product name (line 107-108), returns immediately for Paid (109-112), and only Pending orders query the gateway (114). The success test checks `payments.StatusCalls == 1`, `email.Calls == 1`, and `unitOfWork.SaveCalls == 2` after two completion calls. The failure test checks status calls one, email zero, and saves two. The second call does not invoke `MarkPaid` or `MarkPaymentFailed`; terminal state is the persisted guard.

## Solution 8: ledger and limitation

Invalid customer: zero provider, zero inventory/coupon/order mutation. Sold-out/inactive: zero provider, stock unchanged. Expired coupon: zero provider, redemption and stock unchanged. Provider Failed: one status query, one order save, no email. EF save failure after provider and tracked mutation remains a residual consistency risk because no distributed transaction or compensation exists.

## Solution 9: proposed extension

A production command would authenticate creator and customer, validate an idempotency key, reserve stock with a conditional database update, create or reuse a provider intent, and persist an order plus outbox event in a transaction. A webhook would verify provider signatures before completing. The current app does none of this; its demo creator mutation routes lack authentication and download checks only paid order ID. Keep those follow-up risks in the review.

## The executable fixture

The full fixture is the checked-in file
[`tests/Netgum.Tests/Application/CheckoutServiceTests.cs`](../tests/Netgum.Tests/Application/CheckoutServiceTests.cs).
An earlier version of this chapter pasted all 290 lines here; that copy was
removed on 2026-10-06 because it duplicated the file verbatim and would drift
from it. What to read in it:

| Lines | What |
|---|---|
| 22-61 | `Checkout_session_creates_pending_order_and_completion_fulfills_it` (no variant, no coupon; total 51.45) |
| 63-80 | `InvalidCustomerInput_DoesNotCallGatewayOrReserve` (`[Theory]`, 2 rows) |
| 82-117 | `SoldOutVariant_...` and `InactiveVariant_...` |
| 119-136 | `ExpiredCoupon_DoesNotCallGatewayOrRedeemCoupon` |
| 138-186 | `Completion_IsTerminalAndSendsReceiptOnlyOnce` and `FailedCompletion_IsTerminalWithoutDuplicateProviderCall` |
| 188-243 | helpers: `CreateDbContext`, two `CreateService` overloads, `AddPublishedProduct(dbContext, int? stock, bool withValidCoupon = false)` |
| 245-283 | `CountingPaymentGateway`, `CountingEmailSender`, `CountingUnitOfWork` |

Together with `Domain/EmailAddressTests.cs` (3) and `Domain/ProductTests.cs`
(3), the project has 14 test cases: 8 application, 6 domain.

## Lab answers

Lab A and Lab B answers are written into the **Check** blocks of chapter 05,
because the point of both labs is to compare a written prediction with a run.
The short version: moving `ReserveInventory` alone turns nothing red, since
`CheckoutService.cs:38` already rejects the inputs that could make
`CreatePending` throw; the snapshot service turns 6 of the 8 checkout cases red
and leaves the happy path and expired-coupon cases green.

## Complete repeated-completion assertions

When diagnosing duplicate work, assert the state and every boundary counter together. In `Completion_IsTerminalAndSendsReceiptOnlyOnce`, the first call observes `Succeeded`, marks the order paid, saves, and sends the receipt. The second call reads the same order and returns before `GetPaymentIntentStatusAsync`. Thus `StatusCalls` is one, `email.Calls` is one, and `SaveCalls` is two: the session save plus the first completion save. The product and coupon scalar assertions prove that the second call did not reserve or redeem again. For the failed case, `MarkPaymentFailed` is reached only once, and a second call returns the failed receipt without a third save.

If a future repository implementation returns a detached order from a new context, the same contract still requires the terminal status check before provider work. The current test intentionally uses the actual EF repository and InMemory provider, so it catches mapping and state-transition mistakes. It cannot prove two simultaneous requests are serialized, and it cannot prove that a provider intent is cancelled after a database outage. Those claims belong in an integration design with a transaction, conditional stock update, provider idempotency key, and outbox.

## What a receipt retry cannot guarantee yet

The service saves Paid before sending the receipt. If the sender throws, the order is already paid; a later call returns early and does not retry the email. Two callers that both loaded Pending can also reach provider/save/email work concurrently because no database version predicate coordinates them. The fourteen tests cover sequential service calls, not either failure window. A future outbox should persist a unique order-paid delivery record with the state change and let a retrying worker record delivery attempts; provider idempotency and database concurrency need separate tests. Do not move the email before SaveChanges as a shortcut: that creates a receipt for an order whose paid state may never commit.

Product names are read from the current product on each successful receipt response; they are not frozen on the order. Sequential equivalence in the test assumes the product name has not changed. Coupon eligibility uses the captured command time both before and after the provider await, and the second preflight inspects the same tracked object. It neither refreshes another DbContext's writes nor establishes wall-clock expiry at payment-provider completion.

## Verified review findings outside the repair (checked 2026-10-06)

Statically verified against the current source; none was executed.

**A. "Not found" is reported as 400.** `CheckoutService.cs:42` (product) and
`:98` (order for payment intent) throw `DomainException`, and the global
handler in `src/Netgum.Api/Program.cs:52-57` maps every `DomainException` to
400 "Business rule violation". A client cannot tell a wrong slug from a sold-out
variant. `Payment has not succeeded yet.` (line 124) is also a 400, although it
is the one retryable outcome. A reviewer should ask for a typed error (or a
dedicated exception) per category before a real client depends on these codes.

**B. The checkout `Location` header points at a route that does not exist.**
`CheckoutEndpoints.cs:17` returns `Results.Created($"/api/orders/{checkout.OrderId}", ...)`,
but `OrderEndpoints.cs:10-36` maps only `GET /api/orders/{orderId}/download`.
Following the header yields 404. Either add a `GET /api/orders/{orderId}` or
return the download/status route that exists.

**C. Completion is triggered by any caller who knows a payment-intent ID.**
`POST /api/checkout/complete` (`CheckoutEndpoints.cs:22-31`) takes only
`PaymentIntentId`, with no signature or caller check. With the fake gateway
this is harmless; with a real provider it is the webhook-verification gap that
Solution 9 proposes closing.
