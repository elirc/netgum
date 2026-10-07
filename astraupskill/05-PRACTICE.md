# 5. Practice

Use `tests/Netgum.Tests/Application/CheckoutServiceTests.cs` as the fixture source and chapter 06 as the matched solution. Keep the real request records, repositories, counting doubles, and EF context. Every negative exercise should assert both no side effect and the expected call counts.

Each exercise ends with a **Check**: either an existing test that already encodes the answer (run it with `dotnet test tests/Netgum.Tests/Netgum.Tests.csproj --configuration Release --filter FullyQualifiedName~<TestName>`, with `DOTNET_ROLL_FORWARD=Major` if only .NET 10 is installed) or the condition your new test must meet. The counting doubles expose `payments.CreateCalls`, `payments.StatusCalls`, `email.Calls` and `unitOfWork.SaveCalls` (`CheckoutServiceTests.cs:247-279`).

## Exercise 1: invalid email (predict first)

Create a published product with a stocked variant (`stockQuantity: 3`) and a coupon that has not expired. **Before you run anything**, write these five numbers down on paper for a call to `CreateSessionAsync` with `invalid-email`:

| Value after the call | Your prediction |
|---|---|
| `payments.CreateCalls` | |
| `coupon.RedeemedCount` | |
| `product.Variants.Single().StockQuantity` | |
| `await dbContext.Orders.CountAsync()` | |
| `email.Calls` (the `CountingEmailSender` counter) | |

Now write the test, assert `DomainException`, and assert each of the five values. Diff your predictions against what the run reports. If one was wrong, find the line in `CheckoutService.CreateSessionAsync` that decides it before reading chapter 06.

**Check:** the five values are 0, 0, 3, 0, 0, and the existing `InvalidCustomerInput_DoesNotCallGatewayOrReserve` (the `invalid-email` row) stays green. The deciding line is `CheckoutService.cs:38`.

## Exercise 2: blank name

Repeat with a valid email and empty customer name. Explain why `Order.ValidateCustomer` must run before loading a gateway or mutating a product.

**Check:** the second row (`"reader@example.com", ""`) of `InvalidCustomerInput_DoesNotCallGatewayOrReserve` passes; your explanation cites `Order.ValidateCustomer` (`Order.cs:129`) and its call at `CheckoutService.cs:38`.

## Exercise 3: unavailable variants

Run two cases: stock zero and `variant.Deactivate()`. Assert both fail before payment creation and preserve stock. Include a positive stocked variant case so a universal rejection cannot pass.

**Check:** `SoldOutVariant_DoesNotCallGatewayOrChangeTrackedInventory` and `InactiveVariant_DoesNotCallGatewayOrChangeTrackedInventory` are the reference answers; your positive case must assert `payments.CreateCalls == 1` and stock decremented by one.

## Exercise 4: expired coupon (predict first)

Add a coupon expiring before the fixed clock (2026-04-01 12:00 UTC). **Before running**, predict the same five values as exercise 1 and also predict *which line throws* — name the method. Then check out with the expired code, assert no provider call, no redemption, no stock decrement, and no order, and compare the coupon’s `RedeemedCount` before and after. The interesting part of the diff is the difference between exercise 1 and exercise 4: both reject before the gateway, but at different distances from it.

**Check:** the throwing method is `Coupon.CalculateDiscount` (`Coupon.cs:84-87`, reached from `CheckoutService.cs:47`), and `ExpiredCoupon_DoesNotCallGatewayOrRedeemCoupon` is the reference test. This rejection also happened in the *original* service, which computed the discount before the gateway too; Lab B shows the consequence.

## Exercise 5: valid reservation

Use a stocked variant and future coupon. Assert the total, one payment creation, stock decrement, one redemption, and one pending order. Explain why the second post-await preflight cannot claim a distributed lock.

**Check:** a new test with `stock: 1, withValidCoupon: true` and coupon code `save10` asserts `payments.CreateCalls == 1`, stock 0, `RedeemedCount == 1` and one row in `dbContext.Orders`; the first half of `Completion_IsTerminalAndSendsReceiptOnlyOnce` already builds this setup.

## Exercise 6: paid terminal completion

Complete a valid session with provider status Succeeded twice. Assert both receipts are equivalent, provider status is queried once, email is sent once, saves are two total, and the variant is decremented once.

**Check:** `Completion_IsTerminalAndSendsReceiptOnlyOnce` (`CheckoutServiceTests.cs:138-163`) is the reference.

## Exercise 7: failed terminal completion

Configure the counting gateway as Failed. Complete twice. Assert Failed status, no email, one provider status call, and two total saves. Explain why `MarkPaymentFailed` is not called on the repeat.

**Check:** `FailedCompletion_IsTerminalWithoutDuplicateProviderCall` is the reference. Your explanation cites the early return at `CheckoutService.cs:102-105` and says what `Order.MarkPaymentFailed` (`Order.cs:157-162`) would do if it *were* called on a Failed order: it throws.

## Exercise 8: failure ledger

Write a table with operation, first possible failure, provider calls, tracked mutations, persistence, and expected error. Include invalid email, sold-out variant, expired coupon, provider failure, and EF save failure as a limitation.

**Check:** every row except EF save failure is backed by a named test above; the EF save failure row is labelled untested (nothing makes `CountingUnitOfWork` throw). Compare with Solution 8.

## Exercise 9: proposed production extension

Design an idempotency-key checkout command with creator/customer authorization, conditional stock update, provider webhook verification, and an outbox receipt. Mark it proposed because this project uses fake payment, console email, and EF InMemory tests.

**Check:** for each of the five parts the design names the test that would prove it and the infrastructure that test needs (relational database, signed-webhook fixture, outbox worker). No code is expected.

## Lab A: break the ordering on purpose

A reorder that *looks* dangerous can be invisible to the suite because an earlier guard already covers it. Do the three steps in order, writing a prediction before each run of

```powershell
dotnet test tests/Netgum.Tests/Netgum.Tests.csproj --configuration Release --filter FullyQualifiedName~CheckoutServiceTests
```

1. Move `product.ReserveInventory(request.VariantId);` (`CheckoutService.cs:78`) above `Order.CreatePending(...)` (line 65), as in `snapshots/CheckoutService-before.cs.txt`. Predict which tests go red.
2. Restore the file, then delete only `Order.ValidateCustomer(request.CustomerEmail, request.CustomerName);` (line 38). Predict again.
3. Keep line 38 deleted *and* apply the move from step 1. Predict again.

**Check (derived from the source on 2026-10-06, not from a recorded run; confirm with yours):**

- Step 1: all 8 checkout tests stay green. Every input that could make `CreatePending` throw after reservation (bad email, blank name) is already rejected at line 38, and the remaining `CreatePending` guards (empty IDs, blank payment intent, `Order.cs:100-111`) are unreachable from these fixtures.
- Step 2: both `InvalidCustomerInput_DoesNotCallGatewayOrReserve` rows go red on `payments.CreateCalls` (expected 0, found 1). Stock is still 2, because `CreatePending` re-runs `ValidateCustomer` before line 78.
- Step 3: the same two rows go red one assertion earlier, on `StockQuantity` (expected 2, found 1).

The lesson: the reorder is defense in depth, and only a test that asserts tracked state *and* runs without the earlier guard can see it. Restore with `git checkout -- src/Netgum.Application/Checkout/CheckoutService.cs` and re-run to green.

## Lab B: red-first from the snapshots

`snapshots/` holds the exact pre-change files. Run the suite red on purpose, then fix it back:

1. Copy `snapshots/CheckoutService-before.cs.txt` over `src/Netgum.Application/Checkout/CheckoutService.cs`, and `snapshots/EmailAddress-before.cs.txt` over `src/Netgum.Domain/ValueObjects/EmailAddress.cs`.
2. Run `dotnet build Netgum.sln -c Release`. **It will not compile** — record the exact compiler error and its line. That is the prerequisite repair described in chapter 03.
3. Fix only the `throw ... from ex;` line (write `throw new DomainException("Email address is not valid.", ex);`) and build again. Now the solution compiles and you are looking at the original checkout behaviour.
4. Run the focused filter above and record every failing test name and message.

   **Check (derived from the source on 2026-10-06; confirm with your run):** 6 of the 8 checkout cases go red and 2 stay green.
   - Red: both `InvalidCustomerInput` rows (`StockQuantity` expected 2, found 1, because the old code reserved before `CreatePending` threw); `SoldOutVariant` and `InactiveVariant` (`payments.CreateCalls` expected 0, found 1); `Completion_IsTerminalAndSendsReceiptOnlyOnce` (`payments.StatusCalls` expected 1, found 2, because the old completion re-queried the provider); `FailedCompletion_IsTerminalWithoutDuplicateProviderCall` (the second call throws `DomainException` "Only pending orders can fail payment." from `Order.MarkPaymentFailed`).
   - Green: `Checkout_session_creates_pending_order_and_completion_fulfills_it`, and `ExpiredCoupon_DoesNotCallGatewayOrRedeemCoupon`, because the old service also computed the discount before the gateway.
   - The six domain tests (`EmailAddressTests`, `ProductTests`) do not touch `CheckoutService`.
5. Restore the delivered files and re-run to green. Write one sentence per failing test saying which line of the repair made it pass.

Keep the recorded red output; chapter 06 assumes you have seen it.
