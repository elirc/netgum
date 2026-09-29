# Architecture

Netgum uses a clean architecture style that keeps business rules independent from HTTP, SQL Server, and vendor integrations.

## Dependency Direction

```text
Netgum.Api -> Netgum.Application -> Netgum.Domain
             Netgum.Infrastructure -> Netgum.Application + Netgum.Domain
```

The API hosts the process and maps HTTP requests. The application layer defines use cases and ports. Infrastructure implements those ports with EF Core, logging, fake payments, and fake email. The domain owns marketplace rules.

## Main Use Cases

- Register a creator with a unique slug.
- Create and publish a digital product after at least one asset is attached.
- Add coupons with percentage or fixed-amount discounts.
- Create checkout sessions for published products.
- Complete fake payments and fulfill orders with generated license keys.
- Guard downloads behind a paid order status.

## Extension Points

- Replace `FakePaymentGateway` with a Stripe or Adyen adapter.
- Replace `ConsoleEmailSender` with a transactional email provider.
- Add authentication and require creator ownership on product management endpoints.
- Add webhook endpoints and verify signatures before completing orders.
- Add EF Core migrations once the SDK is available.
