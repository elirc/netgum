using FluentAssertions;
using Xunit;

using Netgum.Domain.Abstractions;
using Netgum.Domain.Products;
using Netgum.Domain.ValueObjects;

namespace Netgum.Tests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Publish_requires_at_least_one_downloadable_asset()
    {
        var product = Product.Create(
            Guid.NewGuid(),
            "Creator Finance Pack",
            "creator-finance-pack",
            "A spreadsheet and guide for creators.",
            Money.Create(29m, "USD"));

        var act = () => product.Publish(DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>()
            .WithMessage("Product must have at least one downloadable asset before publishing.");
    }

    [Fact]
    public void Coupon_uses_the_larger_discount_but_never_exceeds_subtotal()
    {
        var product = Product.Create(
            Guid.NewGuid(),
            "Launch Kit",
            "launch-kit",
            "Templates for shipping a digital product.",
            Money.Create(20m, "USD"));

        var coupon = product.AddCoupon("SAVE", 10, Money.Create(50m, "USD"), 10, null);

        var discount = coupon.CalculateDiscount(Money.Create(20m, "USD"), DateTimeOffset.UtcNow);

        discount.Amount.Should().Be(20m);
        discount.Currency.Should().Be("USD");
    }

    [Fact]
    public void Variant_inventory_is_reserved_one_unit_at_a_time()
    {
        var product = Product.Create(
            Guid.NewGuid(),
            "Design Icons",
            "design-icons",
            "A downloadable icon pack.",
            Money.Create(12m, "USD"));
        var variant = product.AddVariant("Team license", Money.Create(30m, "USD"), stockQuantity: 1);

        product.ReserveInventory(variant.Id);

        var soldOut = () => product.ReserveInventory(variant.Id);
        soldOut.Should().Throw<DomainException>().WithMessage("Variant is sold out.");
    }
}
