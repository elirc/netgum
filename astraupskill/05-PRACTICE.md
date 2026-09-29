# 5. Practice

Use `CheckoutServiceTests.cs` as the fixture source and chapter 06 as the matched solution. Keep the real request records, repositories, counting doubles, and EF context. Every negative exercise should assert both no side effect and the expected call counts.

## Exercise 1: invalid email (predict first)

Create a published product with a stocked variant (`stockQuantity: 3`) and a coupon that has not expired. **Before you run anything**, write these five numbers down on paper for a call to `CreateSessionAsync` with `invalid-email`:

| Value after the call | Your prediction |
|---|---|
| `payments.CreateCalls` | |
| `coupon.RedeemedCount` | |
| `product.Variants.Single().StockQuantity` | |
| `await dbContext.Orders.CountAsync()` | |
| `emails.SentCount` | |

Now write the test, assert `DomainException`, and assert each of the five values. Diff your predictions against what the run reports. If one was wrong, find the line in `CheckoutService.CreateSessionAsync` that decides it before reading chapter 06.

## Exercise 2: blank name

Repeat with a valid email and empty customer name. Explain why `Order.ValidateCustomer` must run before loading a gateway or mutating a product.

## Exercise 3: unavailable variants

Run two cases: stock zero and `variant.Deactivate()`. Assert both fail before payment creation and preserve stock. Include a positive stocked variant case so a universal rejection cannot pass.

## Exercise 4: expired coupon (predict first)

Add a coupon expiring before the fixed clock (2026-04-01 12:00 UTC). **Before running**, predict the same five values as exercise 1 and also predict *which line throws* — name the method. Then check out with the expired code, assert no provider call, no redemption, no stock decrement, and no order, and compare the coupon’s `RedeemedCount` before and after. The interesting part of the diff is the difference between exercise 1 and exercise 4: both reject, but they reject at different distances from the gateway.

## Exercise 5: valid reservation

Use a stocked variant and future coupon. Assert the total, one payment creation, stock decrement, one redemption, and one pending order. Explain why the second post-await preflight cannot claim a distributed lock.

## Exercise 6: paid terminal completion

Complete a valid session with provider status Succeeded twice. Assert both receipts are equivalent, provider status is queried once, email is sent once, saves are two total, and the variant is decremented once.

## Exercise 7: failed terminal completion

Configure the counting gateway as Failed. Complete twice. Assert Failed status, no email, one provider status call, and two total saves. Explain why `MarkPaymentFailed` is not called on the repeat.

## Exercise 8: failure ledger

Write a table with operation, first possible failure, provider calls, tracked mutations, persistence, and expected error. Include invalid email, sold-out variant, expired coupon, provider failure, and EF save failure as a limitation.

## Exercise 9: proposed production extension

Design an idempotency-key checkout command with creator/customer authorization, conditional stock update, provider webhook verification, and an outbox receipt. Mark it proposed because this project uses fake payment, console email, and EF InMemory tests.

## Lab A: break the ordering on purpose

Open `src/Netgum.Application/Checkout/CheckoutService.cs` and move the `product.ReserveInventory(request.VariantId)` call back above `Order.CreatePending(...)`, the way the snapshot at `snapshots/CheckoutService-before.cs.txt` has it. **Predict, in writing, which test names go red and what each failure message will say.** Then run:

```powershell
dotnet test tests/Netgum.Tests/Netgum.Tests.csproj --configuration Release --filter FullyQualifiedName~CheckoutServiceTests
```

Compare the actual failing names and messages with your prediction. The failing assertion should be a stock or coupon counter, not "an exception was thrown" — if a test only checks the exception type it cannot see this bug at all, which is the point of asserting tracked state. Restore the file with `git checkout -- src/Netgum.Application/Checkout/CheckoutService.cs` (or by re-copying the fixed ordering) and re-run to green.

## Lab B: red-first from the snapshots

`snapshots/` holds the exact pre-change files. Run the suite red on purpose, then fix it back:

1. Copy `snapshots/CheckoutService-before.cs.txt` over `src/Netgum.Application/Checkout/CheckoutService.cs`, and `snapshots/EmailAddress-before.cs.txt` over `src/Netgum.Domain/ValueObjects/EmailAddress.cs`.
2. Run `dotnet build Netgum.sln -c Release`. **It will not compile** — record the exact compiler error and its line. That is the prerequisite repair described in chapter 03.
3. Fix only the `throw ... from ex;` line (write `throw new DomainException("Email address is not valid.", ex);`) and build again. Now the solution compiles and you are looking at the original checkout behaviour.
4. Run the focused filter above and record every failing test name and message. Expect the stock/coupon/order-count assertions to fail while the "happy path" test still passes — the original bug only showed up on rejected input.
5. Restore the delivered files and re-run to green. Write one sentence per failing test saying which line of the repair made it pass.

Keep the recorded red output; chapter 06 assumes you have seen it.
