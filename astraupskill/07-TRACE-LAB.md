# 7. Trace lab

Trace the session request:

```http
POST /api/checkout/
Content-Type: application/json

{"creatorSlug":"ada-goods","productSlug":"indie-saas-playbook","variantId":"<guid>","couponCode":"SAVE10","customerEmail":"reader@example.com","customerName":"Reader Person"}
```

The endpoint binds `CreateCheckoutRequest` and resolves `CheckoutService`. The service captures `_clock.UtcNow`, validates customer email/name through `Order.ValidateCustomer`, loads the published aggregate, validates variant state through `Product.ValidatePurchase`, calculates subtotal and coupon discount, then calls the fake gateway. It revalidates product and coupon after the await, constructs a pending `Order` with the returned payment ID, reserves inventory, redeems the coupon, adds the order, and calls the unit of work. A response contains order ID, payment intent ID, checkout URL, and money values.

For an invalid email, the trace stops before repository or gateway work after the clock capture. For a sold-out or inactive variant, it stops after product load and before provider. For an expired coupon, `CalculateDiscount` stops the path before provider. The failure ledger therefore records zero gateway calls, zero stock/coupon mutation, and zero order save for these deterministic cases.

Trace completion with `POST /api/checkout/complete` and a payment intent ID. The service loads the order and product name. A Paid or Failed order returns its receipt immediately. A Pending order asks `IPaymentGateway` for status. Failed status calls `MarkPaymentFailed`, saves once, and returns a cancelled receipt. Succeeded status calls `MarkPaid`, saves once, sends `IEmailSender`, and returns a fulfilled receipt. A second request sees terminal state and does none of those provider, save, or email actions.

The trace ends at fake infrastructure. EF InMemory tracks the aggregate and does not model SQL isolation. Payment and email are local doubles. Product mutation routes still lack creator authorization, and a paid order ID is sufficient for download. A future authenticated command and webhook would need to add those checks. The current evidence proves ordering and terminal idempotency within this application boundary only.

## A concrete trace with values

Assume the product price is 49.00 USD, the selected variant has one unit, and `SAVE10` grants ten percent with a maximum of five redemptions and no fixed-dollar discount. The service calculates a 4.90 USD discount, a 44.10 USD discounted subtotal, and a 2.205 USD platform fee. Money.Create rounds 2.205 away from zero to 2.21, producing 46.31 USD total. The existing no-coupon success case asserts 51.45 USD; this coupon trace is a worked calculation, while its repeated-completion test asserts the side-effect counters and stock/redemption state. The fake gateway receives that total and returns a nonempty `pi_test` identifier. Only after the second availability check does the variant move from one to zero and the coupon redemption count move from zero to one. The response exposes the order and payment identifiers so the caller can invoke completion.

To repeat this trace locally, first create the aggregate fixture and capture `variant.Id` from `product.Variants.Single()`. Pass that ID into `CreateCheckoutRequest`, then inspect `CountingPaymentGateway.CreateCalls` immediately after session creation. A value of one plus a nonempty payment ID proves the positive path. Change only the email to `invalid-email` and run the same operation in a fresh context: the expected exception occurs before the repository query and the counter remains zero. Change only stock to zero or deactivate the variant: the product loads, the preflight rejects it, and the tracked scalar remains unchanged. This sequence gives a useful debugger breakpoint order: customer validation, product lookup, `ValidatePurchase`, discount calculation, provider await, second preflight, order construction, reservation, redemption, save.

For completion, keep the payment ID from the session. The first call with a successful fake status changes the receipt status and sends one email. The second call should show the terminal early return in the debugger. If `StatusCalls` becomes two, the early return is below the provider call. If `email.Calls` becomes two, inspect whether a terminal call can reach the email send instead of returning from the guard. If stock changes twice, the mutation has leaked into completion and belongs only in session creation. These observations connect each assertion to one line of the service rather than treating the test as a black box.
