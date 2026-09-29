# 4. Testing and debugging

**What you need installed:** the .NET SDK pinned in `global.json` (10.0.302) and, because the projects target `net8.0`, either a .NET 8 runtime or `DOTNET_ROLL_FORWARD=Major` in the environment when you run `dotnet test`. Nothing else — no database server, no Docker, no Node.

Use EF Core InMemory to build a real product aggregate and repositories, then replace only external boundaries with counting doubles. This keeps `ProductRepository`, `OrderRepository`, `Order`, `Product`, and EF tracking in the path. A mock that never loads a real aggregate could pass while `Include` or value-object mapping is broken.

The fixture helper creates a creator, published product, downloadable asset, and optional variant. It saves the graph before invoking checkout. The fixed clock is 2026-04-01 12:00 UTC. For customer validation, use `invalid-email` and blank name. For stock validation, make `stock: 0`; for inactive validation, call `variant.Deactivate()`. For an expired coupon, use an expiry before the fixed clock. Each case must assert the exact failure family and zero payment creation calls.

Compare independent tracked state. `product.Variants.Single().StockQuantity` is a scalar before/after check. `coupon.RedeemedCount` catches a discount that was consumed without an order. `dbContext.Orders.CountAsync()` catches an order saved despite a later validation failure. Provider and email counters catch external side effects. Do not compare a newly fetched object to itself or assert only that an exception was thrown.

For completion, configure the fake gateway status explicitly. A successful session creates one pending order. The first succeeded completion changes it to Paid, saves, and emails. The second completion should return an equivalent receipt while status-call, save, and email counters remain unchanged. A failed status changes Pending to Failed once; a second completion returns the terminal receipt without another provider status query. This verifies persisted terminal behavior at the service boundary.

If a test fails after adding preflight, inspect whether the test’s fixed clock makes its coupon already expired or its due date invalid. If a test reports a duplicate email, check the terminal early return before provider status. If inventory changes on invalid input, search for `ReserveInventory` before `Order.CreatePending`. If an inactive variant passes preflight, ensure `ValidateReservation` checks both `IsActive` and stock. If a missing product name changes a paid receipt to “Purchased product,” remember product name is not persisted on Order.

The suite uses fake payment and console email only; there is no real payment, email, or SQL transaction. Release tests can run with `DOTNET_ROLL_FORWARD=Major` when only a newer SDK/runtime is installed. Record that actual runtime condition instead of retargeting the project.

## The shape of one of these tests

This is the customer-validation case described above, from `tests/Netgum.Tests/Application/CheckoutServiceTests.cs:63`. Note what it asserts after the throw: tracked stock, gateway call count, and the order count all have to be unchanged.

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
            "ada-goods", "indie-saas-playbook", product.Variants.Single().Id, null, email, name), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        product.Variants.Single().StockQuantity.Should().Be(2);
        payments.CreateCalls.Should().Be(0);
        (await dbContext.Orders.CountAsync()).Should().Be(0);
    }
```

