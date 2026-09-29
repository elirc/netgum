using Microsoft.Extensions.Logging;

using Netgum.Application.Abstractions;

namespace Netgum.Infrastructure.Email;

public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendPurchaseReceiptAsync(PurchaseReceiptEmail email, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Purchase receipt email queued to {Recipient} for {Product}. License key: {LicenseKey}",
            email.To,
            email.ProductName,
            email.LicenseKey);

        return Task.CompletedTask;
    }
}
