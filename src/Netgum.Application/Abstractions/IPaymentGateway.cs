using Netgum.Domain.ValueObjects;

namespace Netgum.Application.Abstractions;

public interface IPaymentGateway
{
    Task<PaymentIntent> CreatePaymentIntentAsync(PaymentIntentRequest request, CancellationToken cancellationToken);

    Task<PaymentIntentStatus> GetPaymentIntentStatusAsync(string paymentIntentId, CancellationToken cancellationToken);
}

public sealed record PaymentIntentRequest(Guid OrderId, Money Amount, string CustomerEmail, string Description);

public sealed record PaymentIntent(string Id, Uri CheckoutUrl, PaymentIntentStatus Status);

public enum PaymentIntentStatus
{
    RequiresPaymentMethod = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3
}
