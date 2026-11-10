using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence.Converters;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary><see cref="DomainActionConverter"/> stores each action in <c>OrderItems.DomainAction</c> by its member name.</summary>
public sealed class DomainActionConverterTests
{
    /// <summary>Each action maps to its member name and back.</summary>
    /// <param name="action">The action.</param>
    /// <param name="stored">The column value.</param>
    [Theory]
    [InlineData(DomainAction.Register, "Register")]
    [InlineData(DomainAction.Transfer, "Transfer")]
    [InlineData(DomainAction.Renew, "Renew")]
    public void RoundTrip_UsesTheMemberName(DomainAction action, string stored)
    {
        DomainActionConverter.ToStored(action).Should().Be(stored);
        DomainActionConverter.FromStored(stored).Should().Be(action);
    }

    /// <summary>A text that is not exactly a member name fails the read.</summary>
    /// <param name="stored">The column value.</param>
    [Theory]
    [InlineData("register")]
    [InlineData("release")]
    [InlineData("")]
    public void FromStored_UnknownText_Throws(string stored)
    {
        var act = () => DomainActionConverter.FromStored(stored);

        act.Should().Throw<InvalidOperationException>();
    }
}
