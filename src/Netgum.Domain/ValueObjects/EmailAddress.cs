using System.Net.Mail;

using Netgum.Domain.Abstractions;

namespace Netgum.Domain.ValueObjects;

public sealed record EmailAddress
{
    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EmailAddress Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Email address is required.");
        }

        try
        {
            var address = new MailAddress(value.Trim());
            return new EmailAddress(address.Address.ToLowerInvariant());
        }
        catch (FormatException ex)
        {
            // Preserve the parser failure as the inner exception. In C# the idiom is the
            // two-argument constructor (`new DomainException(message, ex)`); there is no
            // `throw ... from ex` syntax (that is Python).
            throw new DomainException("Email address is not valid.", ex);
        }
    }

    public override string ToString() => Value;
}
