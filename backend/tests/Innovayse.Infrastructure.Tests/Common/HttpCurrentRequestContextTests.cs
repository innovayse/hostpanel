namespace Innovayse.Infrastructure.Tests.Common;

using System.Security.Claims;
using FluentAssertions;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Infrastructure.Common;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

/// <summary>
/// Tests for the request context that every audit row is written from.
///
/// <para>
/// What is worth pinning here is that it reads the caller out of a token issued by
/// either mode. The two deliver the same facts under different claim names — SSO mode
/// turns inbound claim mapping off, so "sub" and "email" arrive as themselves, while
/// local mode leaves the mapping on and they arrive renamed. Reading only one spelling
/// returns null against the other, and because these values are what an audit row
/// records, the failure is silent: the action still succeeds, attributed to nobody.
/// </para>
/// </summary>
public sealed class HttpCurrentRequestContextTests
{
    private const string Subject = "8f14e45f-ceea-467a-9575-8b1bcd7d0f5f";

    private static (HttpCurrentRequestContext Context, Mock<IIdentityProvider> Identity) Build(
        params Claim[] claims)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
        });

        var identity = new Mock<IIdentityProvider>();
        identity.Setup(i => i.FindBySubjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityAccount(Subject, "ada@example.com", "Ada", "Lovelace"));

        return (new HttpCurrentRequestContext(accessor.Object, identity.Object), identity);
    }

    [Fact]
    public void UserId_UnderTheSso_ReadsTheRawSubClaim()
    {
        var (context, _) = Build(new Claim("sub", Subject));

        context.UserId.Should().Be(Subject);
    }

    [Fact]
    public void UserId_UnderLocalAuth_ReadsTheMappedClaim()
    {
        var (context, _) = Build(new Claim(ClaimTypes.NameIdentifier, Subject));

        context.UserId.Should().Be(Subject);
    }

    [Fact]
    public void UserEmail_UnderTheSso_ReadsTheRawEmailClaim()
    {
        var (context, _) = Build(new Claim("email", "ada@example.com"));

        context.UserEmail.Should().Be("ada@example.com");
    }

    [Fact]
    public void UserEmail_UnderLocalAuth_ReadsTheMappedClaim()
    {
        var (context, _) = Build(new Claim(ClaimTypes.Email, "ada@example.com"));

        context.UserEmail.Should().Be("ada@example.com");
    }

    [Fact]
    public void UserName_ComesFromTheIdentityProvider_NotTheToken()
    {
        // The token carries no display name in either mode, which is why this is a
        // lookup at all. Going through the provider is what lets it work where the
        // people live in the SSO — it used to take Identity's UserManager, which is
        // not registered there.
        var (context, _) = Build(new Claim("sub", Subject));

        context.UserName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public void UserName_IsLookedUpOncePerRequest()
    {
        // Every admin action records the name more than once. Where the SSO owns the
        // people that is an HTTP call each time, so the answer is held for the life of
        // the scope.
        var (context, identity) = Build(new Claim("sub", Subject));

        _ = context.UserName;
        _ = context.UserName;
        _ = context.UserName;

        identity.Verify(
            i => i.FindBySubjectAsync(Subject, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void UserName_ForAnAnonymousRequest_IsNullAndAsksNobody()
    {
        var (context, identity) = Build();

        context.UserName.Should().BeNull();
        context.UserId.Should().BeNull();

        identity.Verify(
            i => i.FindBySubjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void UserName_WhenTheSubjectResolvesToNobody_IsNull()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Subject)], "test")),
        });

        var identity = new Mock<IIdentityProvider>();
        identity.Setup(i => i.FindBySubjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityAccount?)null);

        new HttpCurrentRequestContext(accessor.Object, identity.Object)
            .UserName.Should().BeNull();
    }

    [Fact]
    public void RequireUserId_ForAnAuthenticatedCaller_ReturnsTheSubject()
    {
        var (context, _) = Build(new Claim("sub", Subject));

        context.RequireUserId().Should().Be(Subject);
    }

    [Fact]
    public void RequireUserId_WithNoSubject_Refuses()
    {
        // Handlers scope every "my …" read and write to this value. Answering something
        // falsy instead of throwing would scope them to nobody, which reads as an empty
        // account rather than as a refusal.
        var (context, _) = Build();

        context.Invoking(c => c.RequireUserId())
            .Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void IsEmailVerified_WhenTheClaimSaysTrue_IsTrue()
    {
        var (context, _) = Build(new Claim("email_verified", "true"));

        context.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public void IsEmailVerified_WhenTheClaimIsAbsent_IsFalse()
    {
        // An issuer that says nothing about the address has not confirmed it. Treating
        // silence as confirmation is how an unverified account walks through a gate that
        // exists to stop it.
        var (context, _) = Build(new Claim("sub", Subject));

        context.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void IsEmailVerified_WhenTheClaimSaysFalse_IsFalse()
    {
        var (context, _) = Build(new Claim("email_verified", "false"));

        context.IsEmailVerified.Should().BeFalse();
    }
}
