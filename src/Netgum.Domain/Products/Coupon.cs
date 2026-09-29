using Netgum.Domain.Abstractions;
using Netgum.Domain.ValueObjects;

namespace Netgum.Domain.Products;

public sealed class Coupon : Entity<Guid>
{
    private Coupon()
        : base(Guid.Empty)
    {
        Code = string.Empty;
    }

    internal Coupon(
        Guid id,
        string code,
        int? percentOff,
        Money? amountOff,
        int? maxRedemptions,
        DateTimeOffset? expiresAt)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Coupon code is required.");
        }

        if (percentOff is null && amountOff is null)
        {
            throw new DomainException("Coupon must define a percentage or amount discount.");
        }

        if (percentOff is < 1 or > 100)
        {
            throw new DomainException("Percent discount must be between 1 and 100.");
        }

        if (maxRedemptions <= 0)
        {
            throw new DomainException("Coupon max redemptions must be greater than zero.");
        }

        Code = code.Trim().ToUpperInvariant();
        PercentOff = percentOff;
        AmountOff = amountOff;
        MaxRedemptions = maxRedemptions;
        ExpiresAt = expiresAt;
        IsActive = true;
    }

    public Guid ProductId { get; private set; }

    public string Code { get; private set; }

    public int? PercentOff { get; private set; }

    public Money? AmountOff { get; private set; }

    public int? MaxRedemptions { get; private set; }

    public int RedeemedCount { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsActive { get; private set; }

    public bool CanRedeem(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return false;
        }

        if (ExpiresAt is not null && ExpiresAt <= now)
        {
            return false;
        }

        return MaxRedemptions is null || RedeemedCount < MaxRedemptions;
    }

    public Money CalculateDiscount(Money subtotal, DateTimeOffset now)
    {
        if (!CanRedeem(now))
        {
            throw new DomainException("Coupon is not redeemable.");
        }

        var percentDiscount = PercentOff is null
            ? Money.Create(0, subtotal.Currency)
            : subtotal.Multiply(PercentOff.Value / 100m);

        var amountDiscount = AmountOff is null
            ? Money.Create(0, subtotal.Currency)
            : AmountOff.Min(subtotal);

        return percentDiscount.Amount >= amountDiscount.Amount ? percentDiscount : amountDiscount;
    }

    public void MarkRedeemed(DateTimeOffset now)
    {
        if (!CanRedeem(now))
        {
            throw new DomainException("Coupon is not redeemable.");
        }

        RedeemedCount++;
    }

    public void Deactivate() => IsActive = false;
}
