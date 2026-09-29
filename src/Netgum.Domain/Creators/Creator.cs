using Netgum.Domain.Abstractions;
using Netgum.Domain.ValueObjects;

namespace Netgum.Domain.Creators;

public sealed class Creator : Entity<Guid>
{
    private Creator()
        : base(Guid.Empty)
    {
        DisplayName = string.Empty;
        Slug = Slug.Create("temporary-creator");
        SupportEmail = EmailAddress.Create("support@example.com");
    }

    private Creator(Guid id, string displayName, Slug slug, EmailAddress supportEmail)
        : base(id)
    {
        DisplayName = displayName;
        Slug = slug;
        SupportEmail = supportEmail;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string DisplayName { get; private set; }

    public Slug Slug { get; private set; }

    public EmailAddress SupportEmail { get; private set; }

    public string? Bio { get; private set; }

    public string? AvatarUrl { get; private set; }

    public string? PayoutAccountId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Creator Register(string displayName, string slug, string supportEmail)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Creator display name is required.");
        }

        return new Creator(Guid.NewGuid(), displayName.Trim(), Slug.Create(slug), EmailAddress.Create(supportEmail));
    }

    public void UpdateProfile(string displayName, string? bio, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Creator display name is required.");
        }

        DisplayName = displayName.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
    }

    public void ConnectPayoutAccount(string payoutAccountId)
    {
        if (string.IsNullOrWhiteSpace(payoutAccountId))
        {
            throw new DomainException("Payout account id is required.");
        }

        PayoutAccountId = payoutAccountId.Trim();
    }
}
