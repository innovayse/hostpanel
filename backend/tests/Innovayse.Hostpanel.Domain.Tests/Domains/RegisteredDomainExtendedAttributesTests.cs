using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Domain.Tests.Domains;

/// <summary>Unit tests for the registry's extra values a <see cref="RegisteredDomain"/> keeps.</summary>
public sealed class RegisteredDomainExtendedAttributesTests
{
    /// <summary>The ISO 4217 code a test domain is priced in.</summary>
    private const string Currency = "USD";

    /// <summary>The fixed moment of the requests.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The values every test stores.</summary>
    private static readonly DomainExtendedAttributes Values =
        DomainExtendedAttributes.From(new Dictionary<string, string> { ["holder-number"] = "N-1" });

    /// <summary>A registration keeps the values it was sent with, as one JSON text ready to be stored.</summary>
    [Fact]
    public void Register_KeepsTheExtendedAttributes()
    {
        var domain = RegisteredDomain.Register(
            1, "name.alpha", Now, Now.AddYears(1), false, false, Currency, extendedAttributes: Values);

        domain.ExtendedAttributes.Should().Be(Values);
        domain.ExtendedAttributesJson.Should().Be(Values.Serialize());
    }

    /// <summary>A registration with no values keeps none and stores nothing.</summary>
    [Fact]
    public void Register_WithoutValues_StoresNothing()
    {
        var domain = RegisteredDomain.Register(1, "name.alpha", Now, Now.AddYears(1), false, false, Currency);

        domain.ExtendedAttributes.IsEmpty.Should().BeTrue();
        domain.ExtendedAttributesJson.Should().BeNull();
    }

    /// <summary>A transfer keeps the values it was sent with.</summary>
    [Fact]
    public void CreateTransfer_KeepsTheExtendedAttributes()
    {
        var domain = RegisteredDomain.CreateTransfer(1, "name.alpha", Now, Currency, extendedAttributes: Values);

        domain.ExtendedAttributes.Should().Be(Values);
    }

    /// <summary>A contact change replaces the stored values with the ones just sent.</summary>
    [Fact]
    public void ReplaceExtendedAttributes_ReplacesTheStoredValues()
    {
        var domain = RegisteredDomain.Register(
            1, "name.alpha", Now, Now.AddYears(1), false, false, Currency, extendedAttributes: Values);
        var replacement = DomainExtendedAttributes.From(new Dictionary<string, string> { ["holder-number"] = "N-2" });

        domain.ReplaceExtendedAttributes(replacement);

        domain.ExtendedAttributes.Should().Be(replacement);
    }

    /// <summary>Replacing with the empty set clears what was stored.</summary>
    [Fact]
    public void ReplaceExtendedAttributes_WithNothing_ClearsThem()
    {
        var domain = RegisteredDomain.Register(
            1, "name.alpha", Now, Now.AddYears(1), false, false, Currency, extendedAttributes: Values);

        domain.ReplaceExtendedAttributes(DomainExtendedAttributes.Empty);

        domain.ExtendedAttributes.IsEmpty.Should().BeTrue();
        domain.ExtendedAttributesJson.Should().BeNull();
    }
}
