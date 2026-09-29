namespace Netgum.Application.Abstractions;

public interface IEmailSender
{
    Task SendPurchaseReceiptAsync(PurchaseReceiptEmail email, CancellationToken cancellationToken);
}

public sealed record PurchaseReceiptEmail(
    string To,
    string ProductName,
    string LicenseKey,
    string DownloadUrl,
    decimal Total,
    string Currency);
