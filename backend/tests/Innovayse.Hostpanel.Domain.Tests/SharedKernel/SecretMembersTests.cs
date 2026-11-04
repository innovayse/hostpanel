using System.Reflection;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.SharedKernel;

/// <summary>The shared way a record prints itself without its secret members.</summary>
public sealed class SecretMembersTests
{
    /// <summary>A secret value is masked and a plain value is printed, in declaration order.</summary>
    [Fact]
    public void ToString_MasksSecretsAndPrintsPlainMembers()
    {
        var probe = new SecretProbe("alice", "hunter2-password", "tok-12345", true);

        probe.ToString().Should().Be("SecretProbe { Name = alice, Password = ***, Token = ***, HasPassword = True }");
    }

    /// <summary>String interpolation goes through the same <c>ToString</c> and shows no secret either.</summary>
    [Fact]
    public void Interpolation_DoesNotContainTheSecret()
    {
        var probe = new SecretProbe("alice", "hunter2-password", "tok-12345", false);

        $"{probe}".Should().NotContain("hunter2-password").And.NotContain("tok-12345");
    }

    /// <summary>A secret that is not set prints as null, so a missing credential stays visible when diagnosing.</summary>
    [Fact]
    public void ToString_UnsetSecret_PrintsNull()
    {
        var probe = new SecretProbe("alice", "hunter2-password", null, false);

        probe.ToString().Should().Contain("Token = null");
    }

    /// <summary>Hiding the secret from <c>ToString</c> leaves value equality exactly as the record declares it.</summary>
    [Fact]
    public void Equality_StillComparesTheSecret()
    {
        var first = new SecretProbe("alice", "one", null, false);

        first.Should().Be(new SecretProbe("alice", "one", null, false));
        first.Should().NotBe(new SecretProbe("alice", "two", null, false));
    }

    /// <summary>The exact masked text of a secret: its name, then the mask, nothing of the value.</summary>
    [Fact]
    public void ToString_WritesTheMaskExactly()
    {
        new SecretProbe("alice", "hunter2-password", "tok-12345", false).ToString().Should().Contain("Password = ***");
    }

    /// <summary>A member with a harmless name is masked once it carries the explicit secret attribute.</summary>
    [Fact]
    public void ToString_MasksAMemberMarkedWithTheAttribute()
    {
        var probe = new SecretAttributeProbe("alice", "123456");

        probe.ToString().Should().Be("SecretAttributeProbe { Name = alice, Code = *** }");
        SecretMembers.IsSecret(typeof(SecretAttributeProbe).GetProperty(nameof(SecretAttributeProbe.Code))!).Should().BeTrue();
    }

    /// <summary>Printing the same type twice gives the same text, so the cached member list behaves like the first pass.</summary>
    [Fact]
    public void ToString_IsStableAcrossRepeatedCalls()
    {
        var probe = new SecretAttributeProbe("alice", "123456");

        probe.ToString().Should().Be(probe.ToString());
    }

    /// <summary>A string named after a secret is a secret; a flag named after one only describes it.</summary>
    /// <param name="property">The property on the probe.</param>
    /// <param name="expected">Whether it holds a secret.</param>
    [Theory]
    [InlineData(nameof(SecretProbe.Name), false)]
    [InlineData(nameof(SecretProbe.Password), true)]
    [InlineData(nameof(SecretProbe.Token), true)]
    [InlineData(nameof(SecretProbe.HasPassword), false)]
    public void IsSecret_ClassifiesByNameAndType(string property, bool expected)
    {
        var info = typeof(SecretProbe).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!;

        SecretMembers.IsSecret(info).Should().Be(expected);
    }
}
