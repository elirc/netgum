# Netgum

Netgum is a learning-oriented, production-shaped clone of Gumroad built with C# and .NET 8. It is not a real commerce product and intentionally uses fake payment and email adapters so you can study the code without vendor accounts.

## What Is Included

- Clean architecture layout: Domain, Application, Infrastructure, API, Tests.
- Creator registration and profile primitives.
- Digital product listings with slugs, prices, assets, variants, publishing, archiving, and coupons.
- Checkout flow with platform fee calculation, fake payment intent creation, order fulfillment, license key generation, and purchase receipt email abstraction.
- EF Core SQL Server persistence mappings, repositories, health checks, Swagger, rate limiting, CORS, structured logging, and RFC7807-style errors.
- xUnit tests for domain rules and the checkout flow.

## Astra Upskill Course

The source-grounded checkout lifecycle course is in [`astraupskill/README.md`](astraupskill/README.md). It walks through the preflight boundary, external payment wait, inventory and coupon mutation, terminal completion, and the limits of the in-memory test fixture.

## Projects

```text
src/Netgum.Domain          Business model and invariants
src/Netgum.Application     Use cases, DTOs, and ports
src/Netgum.Infrastructure  EF Core, repositories, fake payment/email adapters
src/Netgum.Api             Minimal API endpoints and hosting
tests/Netgum.Tests         Focused unit/integration-style tests
```

## Run Locally

With the .NET 8 SDK installed, restore, test, and run the API from this folder:

```powershell
dotnet restore
dotnet test
dotnet run --project src/Netgum.Api
```

The API will expose Swagger at:

```text
https://localhost:7042/swagger
```

## Example Flow

1. `POST /api/creators` to register a creator.
2. `POST /api/products` to create a digital product and attach the first downloadable asset.
3. `POST /api/products/{productId}/publish` to make it purchasable.
4. `POST /api/checkout` with creator/product slugs and customer details.
5. `POST /api/checkout/complete` with the fake payment intent id.
6. `GET /api/orders/{orderId}/download` to receive the licensed download URL.

## Production Notes

For a real product, replace the fake payment gateway with Stripe/Adyen/etc., replace console email with a transactional email provider, add authentication/authorization policies, add migrations, wire blob storage for product assets, add webhook signature verification, and run a proper threat model around fulfillment and download authorization.
