# Feature User Stories

These stories extend Netgum from a learning marketplace into a fuller production-style platform. Each plan assumes the existing clean architecture boundaries:

- Domain rules live in `src/Netgum.Domain`.
- Use cases and ports live in `src/Netgum.Application`.
- EF Core and external adapters live in `src/Netgum.Infrastructure`.
- HTTP endpoints live in `src/Netgum.Api`.
- Tests live in `tests/Netgum.Tests`.

## 1. Creator Authentication

**User story:** As a creator, I want to sign in securely so that only I can manage my storefront and products.

**Acceptance criteria:**

- A creator can register with email and password.
- A creator can sign in and receive an access token.
- Product management endpoints reject anonymous requests.
- Product management endpoints reject authenticated users who do not own the product.
- Passwords are never stored in plain text.

**Implementation plan:**

1. Add an `Auth` folder to `Netgum.Domain`.
2. Create a `UserAccount` entity with `Id`, `EmailAddress`, `PasswordHash`, `CreatorId`, `CreatedAt`, and `LastLoginAt`.
3. Add domain methods such as `Create`, `RecordLogin`, and any validation needed around email ownership.
4. In `Netgum.Application`, create `IUserAccountRepository`, `IPasswordHasher`, and `ITokenIssuer` abstractions.
5. Add request/response records: `RegisterAccountRequest`, `LoginRequest`, and `AuthResponse`.
6. Create an `AuthService` with `RegisterAsync` and `LoginAsync`.
7. In `RegisterAsync`, verify the email is not taken, create the creator, hash the password, create the account, save, and return a token.
8. In `LoginAsync`, fetch the account by email, verify the password hash, record login, save, and return a token.
9. In `Netgum.Infrastructure`, add EF Core mappings for `UserAccount`.
10. Implement `UserAccountRepository`.
11. Implement `PasswordHasher` using ASP.NET Core password hashing APIs.
12. Implement `JwtTokenIssuer` using config values for issuer, audience, signing key, and expiry.
13. In `Netgum.Api`, configure JWT authentication and authorization.
14. Add `/api/auth/register` and `/api/auth/login` endpoints.
15. Add a helper that reads the authenticated creator id from claims.
16. Update product create/update/publish/archive/coupon endpoints to require auth.
17. Validate ownership in application services before allowing product mutations.
18. Add appsettings keys for JWT config.
19. Add tests for password hash verification, login failure, token issuance, and product ownership checks.
20. Update `docs/netgum.http` with auth examples and bearer token usage.

## 2. Creator Storefront Pages

**User story:** As a buyer, I want to view a creator storefront so that I can browse all of their published products in one place.

**Acceptance criteria:**

- Buyers can request a storefront by creator slug.
- The response includes creator profile details and published products.
- Draft and archived products are excluded.
- Results are paginated.
- Unknown creator slugs return `404`.

**Implementation plan:**

1. Add `StorefrontResponse` and `StorefrontProductResponse` records in `Netgum.Application/Catalog`.
2. Add a `GetStorefrontAsync(string creatorSlug, int page, int pageSize)` method to `ProductCatalogService`.
3. Extend `ICreatorRepository` with `GetBySlugAsync`.
4. Extend `IProductRepository` with `GetPublishedByCreatorAsync`.
5. Implement both repository methods in `Netgum.Infrastructure/Persistence/Repositories.cs`.
6. Make sure product queries use `AsNoTracking` for read-only storefront data.
7. Normalize `page` and `pageSize` using the same rules as product search.
8. Return `DomainException` or a not-found result when the creator slug is unknown.
9. Add a new endpoint: `GET /api/storefronts/{creatorSlug}`.
10. Include query parameters for `page` and `pageSize`.
11. Use a stable response shape with creator details and a `PagedResult`.
12. Add tests that create one creator with published, draft, and archived products.
13. Assert only published products appear.
14. Add a test for unknown creator slug.
15. Add a test for pagination.
16. Update `docs/netgum.http` with a storefront request.

## 3. Product Reviews

**User story:** As a buyer, I want to review a product I purchased so that future buyers can make better decisions.

**Acceptance criteria:**

- Only buyers with a paid order can review a product.
- A buyer can leave one review per product.
- Reviews include rating, title, body, and creation time.
- Product summaries include average rating and review count.
- Creators cannot edit buyer reviews.

**Implementation plan:**

1. Add `ProductReview` entity under `Netgum.Domain/Products`.
2. Include `Id`, `ProductId`, `OrderId`, `CustomerEmail`, `Rating`, `Title`, `Body`, `CreatedAt`, and optional `UpdatedAt`.
3. Enforce rating range from 1 to 5 in the domain constructor.
4. Add an `Update` method for buyer-owned edits if desired.
5. Add `IReviewRepository` in the application layer.
6. Create `CreateReviewRequest`, `UpdateReviewRequest`, and `ReviewResponse`.
7. Add `ReviewService`.
8. In `CreateReviewAsync`, load the order by id.
9. Check the order is paid and belongs to the product being reviewed.
10. Check no review already exists for that product and customer email or order.
11. Create the review, save, and return the response.
12. Add EF Core mapping for `ProductReview`.
13. Add `DbSet<ProductReview>` to `NetgumDbContext`.
14. Add unique index on `ProductId` and `OrderId`.
15. Implement review repository methods.
16. Add endpoints: `POST /api/products/{productId}/reviews`, `GET /api/products/{productId}/reviews`, and optionally `PUT /api/reviews/{reviewId}`.
17. Update product search projections to include average rating and review count.
18. Add tests for unpaid order rejection, duplicate review rejection, average rating calculation, and successful review creation.

## 4. Product Collections

**User story:** As a creator, I want to group products into collections so that buyers can browse related products together.

**Acceptance criteria:**

- A creator can create, rename, and archive collections.
- A collection has a slug, title, description, and sort order.
- Products can be added to or removed from collections.
- Public collection pages show only published products.
- Collection slugs are unique per creator.

**Implementation plan:**

1. Add `ProductCollection` entity in `Netgum.Domain/Products`.
2. Add fields for `CreatorId`, `Slug`, `Title`, `Description`, `Status`, `SortOrder`, and timestamps.
3. Add a child entity or join entity named `ProductCollectionItem` with `CollectionId`, `ProductId`, and `SortOrder`.
4. Add domain methods: `Create`, `Rename`, `Archive`, `AddProduct`, `RemoveProduct`, and `ReorderProduct`.
5. Add application contracts for creating, updating, and reading collections.
6. Add `ICollectionRepository`.
7. Add `CollectionService` with use cases for management and public reads.
8. Enforce creator ownership when adding products.
9. Add EF Core mappings and unique index on `CreatorId` plus `Slug`.
10. Map the collection item relationship with cascade delete.
11. Implement repository methods for aggregate loading.
12. Add endpoints under `/api/collections`.
13. Add public endpoint `GET /api/storefronts/{creatorSlug}/collections/{collectionSlug}`.
14. Add tests for duplicate slugs, adding products from another creator, archive behavior, and public filtering.
15. Update architecture docs to mention collections as a catalog feature.

## 5. Product Bundles

**User story:** As a creator, I want to sell multiple products as one bundle so that buyers can purchase related content at a discounted price.

**Acceptance criteria:**

- A creator can create a bundle containing two or more products.
- Bundle products must belong to the same creator.
- A bundle has its own price, slug, description, and cover image.
- Buying a bundle grants access to all bundled product assets.
- Product-level coupons do not automatically apply to bundles.

**Implementation plan:**

1. Decide whether bundles are a new entity or a special product type. Prefer adding `ProductType` to `Product` if bundles should behave like products in listings.
2. Add `ProductType` enum with `Single` and `Bundle`.
3. Add a `BundledProduct` child entity with `BundleId` and `ProductId`.
4. Add domain method `AddBundledProduct`.
5. Enforce at least two bundled products before publishing a bundle.
6. Enforce all bundled products share the same `CreatorId`.
7. Update product creation contracts to optionally specify `ProductType`.
8. Add bundle-specific application use cases: `CreateBundleAsync`, `AddProductToBundleAsync`, and `RemoveProductFromBundleAsync`.
9. Add EF Core mapping for bundled products.
10. Update product repository aggregate includes.
11. Update checkout to detect bundle purchases.
12. Create an order fulfillment model that can expose multiple assets for a single order.
13. Update download endpoint to return all assets for bundle orders.
14. Add tests for bundle publishing rules, creator ownership, checkout totals, and download access.
15. Update docs with bundle behavior and limitations.

## 6. Subscriptions

**User story:** As a buyer, I want to subscribe to a creator or product so that I can receive ongoing content for a recurring fee.

**Acceptance criteria:**

- Creators can define monthly or yearly subscription products.
- Buyers can create a subscription checkout.
- Subscription records track active, past due, canceled, and expired states.
- Active subscribers can access subscription content.
- Canceled subscriptions stop renewing but remain active until the paid-through date.

**Implementation plan:**

1. Add `BillingInterval` enum with `Monthly` and `Yearly`.
2. Add `SubscriptionStatus` enum.
3. Add `Subscription` entity under a new `Subscriptions` domain folder.
4. Include `ProductId`, `CreatorId`, `CustomerEmail`, `PaymentProviderSubscriptionId`, `Status`, `CurrentPeriodStart`, `CurrentPeriodEnd`, and timestamps.
5. Add domain methods: `Activate`, `MarkPastDue`, `CancelAtPeriodEnd`, `Expire`, and `Renew`.
6. Update `Product` to support one-time or recurring pricing.
7. Add application ports for subscription payment gateway methods.
8. Create `SubscriptionService` with create, cancel, and webhook-driven update use cases.
9. Add EF Core mappings and indexes for provider subscription id and customer email.
10. Add endpoints: `POST /api/subscriptions/checkout`, `POST /api/subscriptions/{id}/cancel`, and `GET /api/subscriptions/{id}`.
11. Add authorization checks so buyers can only see their own subscriptions after auth exists.
12. Update download/access checks to allow active subscription access.
13. Add tests for activation, cancellation, period expiry, and access rules.
14. Document fake recurring payment behavior until a real provider is implemented.

## 7. Creator Analytics Dashboard

**User story:** As a creator, I want analytics for sales, revenue, conversion, and products so that I can understand my business.

**Acceptance criteria:**

- Creators can view total revenue, orders, average order value, and refund rate.
- Metrics can be filtered by date range.
- Results only include the authenticated creator's data.
- Product-level breakdowns are available.
- The endpoint is read-optimized and does not load full aggregates unnecessarily.

**Implementation plan:**

1. Add analytics DTOs in `Netgum.Application/Analytics`.
2. Create `AnalyticsQuery` with `CreatorId`, `StartDate`, and `EndDate`.
3. Create `AnalyticsResponse` with totals and product breakdowns.
4. Add `IAnalyticsRepository` or query service abstraction.
5. Implement `AnalyticsService`.
6. Validate date ranges and cap the maximum range if needed.
7. In infrastructure, implement analytics queries with EF Core projections and grouping.
8. Query paid orders only for revenue metrics.
9. Group by `ProductId` and join product names.
10. Add indexes on `orders.creator_id`, `orders.created_at`, and `orders.payment_status` through EF configuration.
11. Add endpoint `GET /api/creator/analytics`.
12. Require authentication and read the creator id from claims.
13. Add tests using seeded orders across multiple creators.
14. Assert one creator cannot see another creator's metrics.
15. Add tests for date filtering and average order value.

## 8. Refunds

**User story:** As a creator, I want to refund an order so that I can support customers when a purchase goes wrong.

**Acceptance criteria:**

- Only paid orders can be refunded.
- A refund stores amount, reason, timestamp, and payment provider refund id.
- Full refunds mark the order as refunded.
- Partial refunds reduce net revenue but do not remove access by default.
- Duplicate refund requests are idempotent.

**Implementation plan:**

1. Add `Refund` entity under `Netgum.Domain/Orders`.
2. Add `RefundStatus` enum if provider state needs tracking.
3. Add a private collection of refunds to `Order`.
4. Add `Order.Refund` domain method with amount validation.
5. Track `RefundedAmount` as a computed domain property.
6. Mark `PaymentStatus` as `Refunded` only when fully refunded.
7. Add `CreateRefundRequest` and `RefundResponse`.
8. Add payment gateway method `CreateRefundAsync`.
9. Add `RefundService` in application layer.
10. Check creator ownership before refunding.
11. Use an idempotency key from the request to prevent duplicate refunds.
12. Add EF Core mapping for refunds with unique index on idempotency key.
13. Add endpoint `POST /api/orders/{orderId}/refunds`.
14. Update analytics to subtract refunds from net revenue.
15. Add tests for full refund, partial refund, over-refund rejection, and duplicate idempotency key.

## 9. Tax Calculation

**User story:** As a marketplace operator, I want to calculate tax during checkout so that orders show accurate totals.

**Acceptance criteria:**

- Checkout accepts buyer country, region, and postal code.
- Tax is calculated before the payment intent is created.
- Orders store tax amount and tax jurisdiction.
- Zero-tax jurisdictions are supported.
- Tax calculation is abstracted behind an application port.

**Implementation plan:**

1. Add `Address` value object with country, region, postal code, and optional city.
2. Add `TaxBreakdown` value object or entity with amount, rate, jurisdiction, and provider reference.
3. Add `TaxAmount` and `TaxJurisdiction` fields to `Order`.
4. Add `ITaxCalculator` in `Netgum.Application.Abstractions`.
5. Add `TaxCalculationRequest` and `TaxCalculationResult`.
6. Implement a fake tax calculator in infrastructure for learning.
7. Update `CreateCheckoutRequest` to include billing address fields.
8. In `CheckoutService`, calculate tax after discount and platform fee rules are clear.
9. Add tax to the payment intent amount and order total.
10. Update response DTOs to include tax.
11. Update EF Core mappings for order tax fields.
12. Add endpoint contract examples to `docs/netgum.http`.
13. Add tests for taxable and non-taxable locations.
14. Add a test that payment intent amount includes tax.
15. Document that real tax compliance needs a provider and legal review.

## 10. File Upload Workflow

**User story:** As a creator, I want a secure upload workflow for product files so that large digital assets are not sent through the API server.

**Acceptance criteria:**

- Creator requests an upload URL for a file.
- The API validates file name, size, and content type.
- The response includes a storage key and pre-signed upload URL.
- Product assets can be attached after upload completion.
- Unsupported content types and oversized files are rejected.

**Implementation plan:**

1. Add `IFileStorage` abstraction in application layer.
2. Define `CreateUploadUrlRequest` and `CreateUploadUrlResponse`.
3. Create `FilePolicyOptions` for max size and allowed content types.
4. Add `UploadService` with `CreateProductAssetUploadAsync`.
5. Validate creator ownership of the target product.
6. Generate a storage key using creator id, product id, and a random file id.
7. Add fake local storage implementation that returns example upload URLs.
8. Later, swap fake storage for S3, Azure Blob Storage, or GCS.
9. Add endpoint `POST /api/products/{productId}/uploads`.
10. Add endpoint `POST /api/products/{productId}/assets` to confirm uploaded files and add `ProductAsset`.
11. Update `ProductCatalogService` if asset confirmation should live there.
12. Add EF Core indexes on product assets by product id.
13. Add tests for file policy validation, ownership, and asset creation.
14. Update docs with upload flow sequence.

## 11. Webhook Processing

**User story:** As a marketplace operator, I want payment webhooks to complete orders reliably so that fulfillment does not depend on the buyer returning to the site.

**Acceptance criteria:**

- Payment provider webhook endpoint accepts signed events.
- Invalid signatures are rejected.
- Duplicate events are ignored safely.
- Successful payment events mark orders paid.
- Failed payment events mark orders failed.

**Implementation plan:**

1. Add `WebhookEvent` entity with provider event id, event type, payload hash, processed status, and timestamps.
2. Add `IWebhookEventRepository`.
3. Add `IWebhookSignatureVerifier` abstraction.
4. Add `PaymentWebhookService` in application layer.
5. In the service, verify signature first.
6. Check whether provider event id has already been processed.
7. Parse event type into known internal commands.
8. For payment success, call order completion logic or a lower-level `MarkOrderPaidAsync`.
9. For payment failure, mark the order failed.
10. Store the webhook event as processed after successful handling.
11. Store failure details if processing throws.
12. Add EF Core mapping with unique index on provider event id.
13. Add API endpoint `POST /api/webhooks/payments`.
14. Keep raw request body available for signature verification.
15. Add tests for invalid signature, duplicate event, success event, and failure event.
16. Update fake payment gateway to produce sample webhook payloads for docs.

## 12. Idempotency Keys

**User story:** As an API consumer, I want idempotent checkout and refund requests so that retries do not create duplicate orders or refunds.

**Acceptance criteria:**

- Client can send an `Idempotency-Key` header.
- Repeating the same key returns the original response.
- Reusing a key with a different request body returns a conflict.
- Keys expire after a configurable retention period.
- Idempotency works for checkout and refunds.

**Implementation plan:**

1. Add `IdempotencyRecord` entity with key, route, request hash, response payload, status code, expiration, and timestamps.
2. Add `IIdempotencyStore` abstraction.
3. Add EF Core mapping and unique index on route plus key.
4. Add middleware or endpoint filter in the API project.
5. Read `Idempotency-Key` from the request headers.
6. Hash the request body using SHA-256.
7. Check for an existing record before executing the endpoint.
8. If the same key and hash exist, return stored response.
9. If the same key has a different hash, return `409 Conflict`.
10. After a successful endpoint result, store response body and status code.
11. Apply the filter only to mutation endpoints such as checkout and refunds.
12. Add cleanup strategy for expired records.
13. Add tests at the application or API level for retry behavior.
14. Document the header in `docs/netgum.http`.

## 13. Creator Payouts

**User story:** As a creator, I want payout tracking so that I can see when my earnings will be transferred.

**Acceptance criteria:**

- Paid orders generate ledger entries.
- Platform fees and refunds are represented in the ledger.
- Payout batches can be created for available balances.
- Payouts track pending, processing, paid, and failed states.
- Creators can view payout history.

**Implementation plan:**

1. Add `LedgerEntry` entity with creator id, order id, type, amount, currency, and timestamp.
2. Add `LedgerEntryType` enum for sale, platform fee, refund, and payout.
3. Add `Payout` entity with creator id, amount, currency, status, provider payout id, and timestamps.
4. Add domain methods for payout state transitions.
5. Add `ILedgerRepository` and `IPayoutRepository`.
6. Add `IPayoutProvider` abstraction.
7. Update checkout completion to create sale and platform fee ledger entries.
8. Update refunds to create refund ledger entries.
9. Add `PayoutService` with `GetBalanceAsync`, `CreatePayoutAsync`, and `ListPayoutsAsync`.
10. Ensure only available cleared funds are paid out.
11. Add EF Core mappings and indexes by creator id and created date.
12. Add endpoints under `/api/creator/payouts`.
13. Require authentication and creator ownership.
14. Add tests for balance calculation, payout creation, insufficient balance, and failed payout transition.
15. Update architecture docs with ledger-based accounting notes.

## 14. Customer Library

**User story:** As a buyer, I want a library of my purchases so that I can find and download products later.

**Acceptance criteria:**

- A buyer can sign in and view paid orders by email/account.
- The library shows product name, creator name, purchase date, and download status.
- Refunded orders can be hidden or marked based on policy.
- Downloads still require valid paid access.
- Results are paginated.

**Implementation plan:**

1. Add buyer account support or reuse `UserAccount` with a role field.
2. Add `AccountRole` enum with `Creator`, `Buyer`, and potentially `Admin`.
3. Add `CustomerLibraryItemResponse`.
4. Add `CustomerLibraryService`.
5. Create repository query method `GetPaidOrdersForCustomerAsync`.
6. Match orders by authenticated account email at first.
7. Later, add explicit order-to-account linking for stronger identity.
8. Join orders to products and creators in a read-optimized projection.
9. Add endpoint `GET /api/me/library`.
10. Require authentication.
11. Update download endpoint to allow the authenticated buyer to download their own purchases.
12. Add tests for library filtering by email, pagination, and download authorization.
13. Document buyer auth assumptions.

## 15. Wishlists

**User story:** As a buyer, I want to save products to a wishlist so that I can come back to them later.

**Acceptance criteria:**

- Authenticated buyers can add and remove published products from a wishlist.
- Adding the same product twice is safe.
- Wishlist lists product summary information.
- Archived products are hidden or marked unavailable.
- Buyers cannot modify another buyer's wishlist.

**Implementation plan:**

1. Add `WishlistItem` entity with account id, product id, and created timestamp.
2. Add unique index on account id plus product id.
3. Add `IWishlistRepository`.
4. Add `WishlistService` with add, remove, and list methods.
5. In `AddAsync`, verify product exists and is published.
6. Treat duplicate adds as success.
7. In `ListAsync`, project product summaries.
8. Add endpoints: `GET /api/me/wishlist`, `POST /api/me/wishlist/{productId}`, and `DELETE /api/me/wishlist/{productId}`.
9. Require buyer authentication.
10. Add tests for duplicate add, unpublished product rejection, removal, and user isolation.
11. Update docs with wishlist examples.

## 16. Product Search Filters

**User story:** As a buyer, I want to filter and sort search results so that I can find products that match my budget and interests.

**Acceptance criteria:**

- Search supports term, minimum price, maximum price, creator slug, rating, and sort.
- Sort options include newest, price ascending, price descending, and rating.
- Invalid filter ranges return a clear validation error.
- Search remains paginated.
- Query results include enough summary data for a product card.

**Implementation plan:**

1. Extend `ProductSearchQuery` with `MinPrice`, `MaxPrice`, `CreatorSlug`, `MinimumRating`, and `Sort`.
2. Add `ProductSort` enum.
3. Validate price ranges in `ProductCatalogService`.
4. Update `IProductRepository.SearchPublishedAsync` implementation.
5. Add optional joins for creator and review aggregates.
6. Apply filters before pagination.
7. Apply stable ordering before `Skip` and `Take`.
8. Update API endpoint query parameters.
9. Add XML or OpenAPI descriptions if desired.
10. Add tests for each filter and sort.
11. Add tests for invalid ranges.
12. Update `docs/netgum.http` with filtered search examples.

## 17. Admin Moderation

**User story:** As an admin, I want to moderate products and creators so that unsafe or spammy content can be removed from the marketplace.

**Acceptance criteria:**

- Admins can suspend creators.
- Admins can unpublish products.
- Suspended creators cannot publish new products.
- Public search excludes suspended creators and moderated products.
- Moderation actions store reason, actor, and timestamp.

**Implementation plan:**

1. Add `CreatorStatus` enum with active and suspended.
2. Add `ModerationStatus` enum for products.
3. Add methods on `Creator` and `Product` for moderation state changes.
4. Add `ModerationAction` entity to audit actions.
5. Add `IModerationRepository`.
6. Add `ModerationService` with suspend creator, reinstate creator, unpublish product, and restore product use cases.
7. Require admin role in API endpoints.
8. Update product publishing to reject suspended creators.
9. Update search and storefront queries to exclude suspended creators and moderated products.
10. Add EF mappings for new fields and audit entity.
11. Add endpoints under `/api/admin/moderation`.
12. Add tests for suspended creator publishing rejection and public query filtering.
13. Add tests for audit record creation.
14. Document moderation rules.

## 18. Notifications

**User story:** As a creator, I want notifications for sales, reviews, and payout events so that I can stay informed.

**Acceptance criteria:**

- Notifications are created for order paid, review created, and payout paid events.
- Creators can list unread and all notifications.
- Creators can mark notifications as read.
- Notification creation is idempotent for the same source event.
- Notification delivery can later support email or push channels.

**Implementation plan:**

1. Add `Notification` entity with creator id, type, title, body, source id, read timestamp, and created timestamp.
2. Add `NotificationType` enum.
3. Add `INotificationRepository`.
4. Add `NotificationService`.
5. Add application methods for creating sale, review, and payout notifications.
6. Add unique index on notification type plus source id to prevent duplicates.
7. Hook notification creation into checkout completion, review creation, and payout completion.
8. Add endpoints: `GET /api/creator/notifications`, `POST /api/creator/notifications/{id}/read`, and `POST /api/creator/notifications/read-all`.
9. Require authentication and creator ownership.
10. Add tests for notification creation, duplicate prevention, listing, and marking read.
11. Add docs explaining in-app notifications and future delivery adapters.

## 19. Audit Logging

**User story:** As an operator, I want audit logs for sensitive actions so that account, product, payout, and refund changes are traceable.

**Acceptance criteria:**

- Sensitive mutations write audit records.
- Audit records include actor id, action, target type, target id, timestamp, and metadata.
- Audit logs are append-only.
- Admins can query audit logs by actor, target, action, and date range.
- Audit logging failure does not silently hide critical errors.

**Implementation plan:**

1. Add `AuditLog` entity in infrastructure or domain depending on whether it is business-visible. Prefer application/infrastructure if it is operational only.
2. Add `IAuditLogWriter` abstraction in application.
3. Add `AuditLogMetadata` as a dictionary or JSON string.
4. Add EF mapping with JSON metadata column if the provider supports it, or plain text for simplicity.
5. Add a reusable helper in application services to write audit records after successful mutations.
6. Record actions for product publish/archive, refund creation, payout changes, moderation, and auth events.
7. Add `IAuditLogReader` for admin queries.
8. Add `AuditLogQuery` and `AuditLogResponse`.
9. Add endpoint `GET /api/admin/audit-logs`.
10. Require admin authorization.
11. Add tests that key mutations create audit logs.
12. Add tests for audit query filters.
13. Document which actions are audited.

## 20. API Versioning

**User story:** As an API consumer, I want stable versioned endpoints so that future breaking changes do not break existing clients.

**Acceptance criteria:**

- Public routes are available under `/api/v1`.
- Existing endpoints can be migrated without changing use-case logic.
- Swagger groups endpoints by version.
- New versions can coexist with old versions.
- Versioning rules are documented.

**Implementation plan:**

1. Decide on URL path versioning for simplicity: `/api/v1/...`.
2. Update endpoint mapping groups to include a version prefix.
3. Create a central `ApiRoutes` helper or constants file to reduce repeated strings.
4. Move current endpoints under `/api/v1/creators`, `/api/v1/products`, `/api/v1/checkout`, and `/api/v1/orders`.
5. Optionally keep temporary unversioned endpoints that redirect or return deprecation headers.
6. Add Swagger document configuration for `v1`.
7. Add endpoint metadata tags that include version.
8. Update `docs/netgum.http` to use `/api/v1`.
9. Update README example flow.
10. Add integration-style tests that expected v1 routes are mapped.
11. Document rules for introducing `/api/v2`.
12. Make sure versioning stays in the API project and does not leak into application services.
