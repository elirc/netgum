using FluentAssertions;
using Xunit;

using Netgum.Domain.Abstractions;
using Netgum.Domain.ValueObjects;

namespace Netgum.Tests.Domain;

public sealed class EmailAddressTests
{
    [Fact]
    public void Create_normalises_a_valid_address()
    {
        var email = EmailAddress.Create("  Buyer@Example.COM ");

        email.Value.Should().Be("buyer@example.com");
    }

    [Fact]
    public void Create_rejects_a_blank_address_without_an_inner_exception()
    {
        var act = () => EmailAddress.Create("   ");

        act.Should().Throw<DomainException>()
            .WithMessage("Email address is required.")
            .And.InnerException.Should().BeNull();
    }

    [Fact]
    public void Create_preserves_the_parser_failure_as_the_inner_exception()
    {
        var act = () => EmailAddress.Create("invalid-email");

        act.Should().Throw<DomainException>()
            .WithMessage("Email address is not valid.")
            .WithInnerException<FormatException>();
    }
}
