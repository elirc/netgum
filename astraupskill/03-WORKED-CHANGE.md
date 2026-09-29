# 3. Worked change

## Prerequisite repair: the baseline did not compile

Before any of the ordering work below could be tested, one file had to be repaired: `src/Netgum.Domain/ValueObjects/EmailAddress.cs`. The original (kept byte-for-byte at `snapshots/EmailAddress-before.cs.txt:27-30`) read:

```csharp
catch (FormatException ex)
{
    throw new DomainException("Email address is not valid.") from ex;
}
```

`throw X from ex` is Python syntax, not C#; the C# compiler rejects it (`CS1002`/`CS1525` at that line), so the solution did not build and no test in this project could run. That is a prerequisite repair, not part of the checkout contract change, and it is called out here so you do not read the delivered file as "the original plus one behavioural fix".

The C# equivalent of Python's `raise ... from ex` is the two-argument exception constructor. The first repair dropped the inner exception entirely (`throw new DomainException("Email address is not valid.");`), which loses the `FormatException` the parser produced and therefore the reason the address was rejected. The shipped code now does what the original author meant:

```csharp
catch (FormatException ex)
{
    throw new DomainException("Email address is not valid.", ex);
}
```

`DomainException` gained the matching `(string message, Exception innerException)` constructor (`src/Netgum.Domain/Abstractions/DomainException.cs`). `EmailAddressTests.Create_preserves_the_parser_failure_as_the_inner_exception` asserts the chain, so a future "simplify" that drops the `ex` argument turns red.

The old `CreateSessionAsync` loaded a published product and calculated subtotal and discount, then called `_payments.CreatePaymentIntentAsync`. After the await it called `product.ReserveInventory`, `coupon.MarkRedeemed`, and `Order.CreatePending`. This order makes several rejected inputs expensive: an invalid email or blank customer name can create a payment intent; sold-out or inactive variants can create an intent; and the order constructor can fail after tracked inventory or coupon changes.

The repaired method starts with `var now = _clock.UtcNow` and `Order.ValidateCustomer(request.CustomerEmail, request.CustomerName)`. It loads the published product, calls `product.ValidatePurchase(request.VariantId)`, calculates subtotal, finds the coupon, and calculates discount at `now`. Platform fee and total are pure money values. No gateway, stock, coupon, or order persistence has happened yet. The invalid customer, sold-out, inactive, and expired-coupon tests assert provider calls are zero and tracked fields remain unchanged.

The gateway receives an order ID and the computed total. After the await, the service calls `product.ValidatePurchase` again and checks `coupon.CanRedeem(now)`. This second check is useful when another request may have changed tracked state, but it cannot undo a provider intent if the state changed concurrently. The limit explicitly says this is not a distributed transaction. Then `Order.CreatePending` receives the payment intent ID and captured time. Only a complete candidate reaches `ReserveInventory`, `MarkRedeemed`, `_orders.Add`, and `SaveChangesAsync`.

The completion repair addresses duplicate side effects. It loads the order, then checks the terminal states in the order the reads require: a Failed order returns the existing failed receipt immediately, before the product query, because that branch answers with the literal `"Unknown product"` and never uses the product name. Only then does the service load the product for the name, and a Paid order returns the receipt immediately. Pending orders still ask the gateway for status. A Failed provider result marks the order failed and saves once. A Succeeded result marks the order paid, saves once, sends one receipt, and returns. Repeating either terminal request does not query provider status, save, or email again.

The test fixture’s `CountingPaymentGateway` records Create and Status calls and returns a configurable status. `CountingEmailSender` records receipt calls. `CountingUnitOfWork` delegates to the real EF context while counting saves. The valid test verifies one payment creation, one status query, one email, two saves (session and first completion), one stock decrement, one redemption, and equivalent repeated receipt. The failed test verifies one status query, no email, and no third save.

This implementation does not compensate a provider intent after a later EF failure. It also does not claim stock concurrency across separate DbContexts. Those are explicit follow-up designs requiring an outbox, transaction, or database conditional update. The bounded change makes deterministic validation and terminal behavior honest and testable.

## Reading the ordering as a failure ledger

The ordering can be reviewed one line at a time. `Order.ValidateCustomer` can fail on either the email parser or the blank-name check; at that point there is no loaded aggregate, provider call, or tracked write. Product lookup can return null when the creator slug or product slug is wrong. `ValidatePurchase` can then reject an unpublished product, a missing variant, an inactive variant, or a variant with no stock. Those failures occur before `CreatePaymentIntentAsync`, so a retry does not leave a payment intent behind. Coupon calculation uses the same captured `now` as the later redemption check. If the coupon is already expired, `CalculateDiscount` throws while all state is still a candidate.

The await is the boundary that deserves special attention. Before it, the service has calculated a total but has not reserved stock. During it, another request could consume the last unit or redeem the last coupon slot. The second `ValidatePurchase` and `CanRedeem(now)` turn that stale candidate into a clear rejection. They do not roll back a provider intent created during the await, which is why the code and course call out compensation as a separate concern. After the second check, the order constructor receives every required value, so the state transition is assembled before `ReserveInventory` and `MarkRedeemed` are called. A code review can therefore ask whether each mutation has a complete order candidate beside it.

Completion has a different ledger. A pending order plus a failed provider status causes one domain transition and one save. A pending order plus success causes one transition, one save, and one receipt. A paid or failed order is already a terminal record, so the early return prevents status polling and all repeat side effects. This is application-level idempotency for the observed terminal state; it does not replace an idempotency key for two simultaneous pending requests. The test counts make those boundaries visible without pretending that the fake provider is Stripe or that EF InMemory is SQL Server.
