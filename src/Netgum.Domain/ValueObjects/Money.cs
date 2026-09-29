using Netgum.Domain.Abstractions;

namespace Netgum.Domain.ValueObjects;

public sealed record Money
{
    public static readonly Money ZeroUsd = new(0m, "USD");

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Create(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new DomainException("Money amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new DomainException("Currency must be a three-letter ISO code.");
        }

        return new Money(decimal.Round(amount, 2, MidpointRounding.AwayFromZero), currency.Trim().ToUpperInvariant());
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return Create(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        if (other.Amount > Amount)
        {
            throw new DomainException("Cannot subtract more money than the current amount.");
        }

        return Create(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal multiplier)
    {
        if (multiplier < 0)
        {
            throw new DomainException("Money multiplier cannot be negative.");
        }

        return Create(Amount * multiplier, Currency);
    }

    public Money Min(Money other)
    {
        EnsureSameCurrency(other);
        return Amount <= other.Amount ? this : other;
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Money values must use the same currency.");
        }
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
