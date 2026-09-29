using Netgum.Application.Abstractions;

namespace Netgum.Infrastructure.Payments;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public Task<PaymentIntent> CreatePaymentIntentAsync(PaymentIntentRequest request, CancellationToken cancellationToken)
    {
        var id = $"fake_pi_{request.OrderId:N}";
        var checkoutUrl = new Uri($"https://payments.example.test/checkout/{id}");
        return Task.FromResult(new PaymentIntent(id, checkoutUrl, PaymentIntentStatus.Processing));
    }

    public Task<PaymentIntentStatus> GetPaymentIntentStatusAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        var status = paymentIntentId.Contains("fail", StringComparison.OrdinalIgnoreCase)
            ? PaymentIntentStatus.Failed
            : PaymentIntentStatus.Succeeded;

        return Task.FromResult(status);
    }
}
