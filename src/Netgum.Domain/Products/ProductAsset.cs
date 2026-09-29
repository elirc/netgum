using Netgum.Domain.Abstractions;

namespace Netgum.Domain.Products;

public sealed class ProductAsset : Entity<Guid>
{
    private ProductAsset()
        : base(Guid.Empty)
    {
        FileName = string.Empty;
        StorageKey = string.Empty;
        ContentType = string.Empty;
    }

    internal ProductAsset(Guid id, string fileName, string storageKey, string contentType, long sizeBytes)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("Asset file name is required.");
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new DomainException("Asset storage key is required.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("Asset size must be greater than zero.");
        }

        FileName = fileName.Trim();
        StorageKey = storageKey.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        SizeBytes = sizeBytes;
        UploadedAt = DateTimeOffset.UtcNow;
    }

    public Guid ProductId { get; private set; }

    public string FileName { get; private set; }

    public string StorageKey { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }
}
