using Netgum.Domain.Orders;

namespace Netgum.Application.Checkout;

public sealed record CreateCheckoutRequest(
    string CreatorSlug,
    string ProductSlug,
    Guid? VariantId,
    string? CouponCode,
    string CustomerEmail,
    string CustomerName);

public sealed record CheckoutSessionResponse(
    Guid OrderId,
    string PaymentIntentId,
    Uri CheckoutUrl,
    decimal Subtotal,
    decimal Discount,
    decimal PlatformFee,
    decimal Total,
    string Currency);

public sealed record CompleteOrderRequest(string PaymentIntentId);

public sealed record OrderReceiptResponse(
    Guid OrderId,
    string ProductName,
    string CustomerEmail,
    string LicenseKey,
    decimal Total,
    string Currency,
    PaymentStatus PaymentStatus,
    OrderStatus OrderStatus);
