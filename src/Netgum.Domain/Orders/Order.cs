using System.Security.Cryptography;

using Netgum.Domain.Abstractions;
using Netgum.Domain.ValueObjects;

namespace Netgum.Domain.Orders;

public sealed class Order : Entity<Guid>
{
    private Order()
        : base(Guid.Empty)
    {
        CustomerEmail = EmailAddress.Create("customer@example.com");
        CustomerName = string.Empty;
        Currency = "USD";
        Subtotal = Money.ZeroUsd;
        Discount = Money.ZeroUsd;
        PlatformFee = Money.ZeroUsd;
        Total = Money.ZeroUsd;
        PaymentIntentId = string.Empty;
        LicenseKey = string.Empty;
    }

    private Order(
        Guid id,
        Guid productId,
        Guid creatorId,
        Guid? variantId,
        EmailAddress customerEmail,
        string customerName,
        Money subtotal,
        Money discount,
        Money platformFee,
        Money total,
        string paymentIntentId,
        DateTimeOffset createdAt)
        : base(id)
    {
        ProductId = productId;
        CreatorId = creatorId;
        VariantId = variantId;
        CustomerEmail = customerEmail;
        CustomerName = customerName;
        Currency = total.Currency;
        Subtotal = subtotal;
        Discount = discount;
        PlatformFee = platformFee;
        Total = total;
        PaymentIntentId = paymentIntentId;
        PaymentStatus = PaymentStatus.Pending;
        Status = OrderStatus.Created;
        LicenseKey = CreateLicenseKey();
        CreatedAt = createdAt;
    }

    public Guid ProductId { get; private set; }

    public Guid CreatorId { get; private set; }

    public Guid? VariantId { get; private set; }

    public EmailAddress CustomerEmail { get; private set; }

    public string CustomerName { get; private set; }

    public string Currency { get; private set; }

    public Money Subtotal { get; private set; }

    public Money Discount { get; private set; }

    public Money PlatformFee { get; private set; }

    public Money Total { get; private set; }

    public string PaymentIntentId { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; }

    public OrderStatus Status { get; private set; }

    public string LicenseKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public static Order CreatePending(
        Guid orderId,
        Guid productId,
        Guid creatorId,
        Guid? variantId,
        string customerEmail,
        string customerName,
        Money subtotal,
        Money discount,
        Money platformFee,
        string paymentIntentId,
        DateTimeOffset now)
    {
        if (orderId == Guid.Empty || productId == Guid.Empty || creatorId == Guid.Empty)
        {
            throw new DomainException("Order, product, and creator ids are required.");
        }

        ValidateCustomer(customerEmail, customerName);

        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            throw new DomainException("Payment intent id is required.");
        }

        var total = subtotal.Subtract(discount).Add(platformFee);
        return new Order(
            orderId,
            productId,
            creatorId,
            variantId,
            EmailAddress.Create(customerEmail),
            customerName.Trim(),
            subtotal,
            discount,
            platformFee,
            total,
            paymentIntentId.Trim(),
            now);
    }

    public static void ValidateCustomer(string customerEmail, string customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new DomainException("Customer name is required.");
        }

        _ = EmailAddress.Create(customerEmail);
    }

    public void MarkPaid(DateTimeOffset now)
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            return;
        }

        if (PaymentStatus != PaymentStatus.Pending)
        {
            throw new DomainException("Only pending orders can be marked paid.");
        }

        PaymentStatus = PaymentStatus.Paid;
        Status = OrderStatus.Fulfilled;
        PaidAt = now;
        Raise(new OrderPaidEvent(Id, ProductId, CustomerEmail.Value, now));
    }

    public void MarkPaymentFailed()
    {
        if (PaymentStatus != PaymentStatus.Pending)
        {
            throw new DomainException("Only pending orders can fail payment.");
        }

        PaymentStatus = PaymentStatus.Failed;
        Status = OrderStatus.Cancelled;
    }

    private static string CreateLicenseKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes).Insert(16, "-");
    }
}
