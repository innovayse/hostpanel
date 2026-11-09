using System.Security.Claims;
using Innovayse.Hostpanel.API.Identity;
using Innovayse.Hostpanel.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Innovayse.Hostpanel.Integration.Tests.Identity;

/// <summary>
/// The adapter every handler learns its caller from, and every audit row is written from.
/// </summary>
/// <remarks>
/// What is worth pinning is that it reads the caller out of a token issued by either mode. The two
/// deliver the same facts under different claim names: single sign-on mode turns inbound claim mapping off, so
/// <c>sub</c> and <c>email</c> arrive as themselves, while local mode leaves the mapping on and they
/// arrive renamed. Reading only one spelling returns null against the other, and because these values
/// are what an audit row records, the failure is silent: the action succeeds, attributed to nobody.
/// </remarks>
public sealed class HttpCurrentUserServiceTests
{
    /// <summary>A subject id in the shape both modes issue.</summary>
    private const string Subject = "8f14e45f-ceea-467a-9575-8b1bcd7d0f5f";

    /// <summary>The account the subject resolves to.</summary>
    private static readonly IdentityAccount Ada = new(Subject, "ada@example.com", "Ada", "Lovelace");

    /// <summary>
    /// Builds the adapter over a request carrying <paramref name="claims"/>.
    /// </summary>
    /// <param name="identity">Where the display name is looked up.</param>
    /// <param name="claims">The claims on the request's principal; none means anonymous.</param>
    /// <returns>The adapter under test.</returns>
    private static HttpCurrentUserService Build(FakeIdentityProvider identity, params Claim[] claims)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                // An authentication type only when there are claims, so "no claims" is also "not
                // authenticated", as it is for a real anonymous request.
                User = new ClaimsPrincipal(claims.Length == 0
                    ? new ClaimsIdentity()
                    : new ClaimsIdentity(claims, "test")),
            },
        };

        return new HttpCurrentUserService(accessor, identity);
    }

    /// <summary>Under the sign-on provider the subject arrives as the raw <c>sub</c> claim.</summary>
    [Fact]
    public void UserId_UnderTheSso_ReadsTheRawSubClaim()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim("sub", Subject));

        service.UserId.Should().Be(Subject);
    }

    /// <summary>Under local auth the subject arrives as the mapped name-identifier claim.</summary>
    [Fact]
    public void UserId_UnderLocalAuth_ReadsTheMappedClaim()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim(ClaimTypes.NameIdentifier, Subject));

        service.UserId.Should().Be(Subject);
    }

    /// <summary>Under the sign-on provider the email arrives as the raw <c>email</c> claim.</summary>
    [Fact]
    public void Email_UnderTheSso_ReadsTheRawEmailClaim()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim("email", "ada@example.com"));

        service.Email.Should().Be("ada@example.com");
    }

    /// <summary>Under local auth the email arrives as the mapped email claim.</summary>
    [Fact]
    public void Email_UnderLocalAuth_ReadsTheMappedClaim()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim(ClaimTypes.Email, "ada@example.com"));

        service.Email.Should().Be("ada@example.com");
    }

    /// <summary>
    /// The token carries no display name in either mode, which is why this is a lookup at all; going
    /// through the provider is what lets it work where the people live in the sign-on provider.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetUserNameAsync_ComesFromTheIdentityProvider_NotTheToken()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim("sub", Subject));

        (await service.GetUserNameAsync(CancellationToken.None)).Should().Be("Ada Lovelace");
    }

    /// <summary>
    /// Every admin action records the name more than once; where the sign-on provider owns the people each lookup is
    /// an HTTP call, so the answer is held for the life of the scope.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetUserNameAsync_IsLookedUpOncePerRequest()
    {
        var identity = new FakeIdentityProvider(Ada);
        var service = Build(identity, new Claim("sub", Subject));

        _ = await service.GetUserNameAsync(CancellationToken.None);
        _ = await service.GetUserNameAsync(CancellationToken.None);
        _ = await service.GetUserNameAsync(CancellationToken.None);

        identity.SubjectLookups.Should().Be(1);
    }

    /// <summary>An anonymous request has no name and asks the provider nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetUserNameAsync_ForAnAnonymousRequest_IsNullAndAsksNobody()
    {
        var identity = new FakeIdentityProvider(Ada);
        var service = Build(identity);

        (await service.GetUserNameAsync(CancellationToken.None)).Should().BeNull();
        service.UserId.Should().BeNull();
        service.IsAuthenticated.Should().BeFalse();
        identity.SubjectLookups.Should().Be(0);
    }

    /// <summary>A subject the provider does not know has no name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetUserNameAsync_WhenTheSubjectResolvesToNobody_IsNull()
    {
        var service = Build(new FakeIdentityProvider(null), new Claim("sub", Subject));

        (await service.GetUserNameAsync(CancellationToken.None)).Should().BeNull();
    }

    /// <summary>An authenticated caller's subject is returned.</summary>
    [Fact]
    public void RequireUserId_ForAnAuthenticatedCaller_ReturnsTheSubject()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim("sub", Subject));

        service.RequireUserId().Should().Be(Subject);
        service.IsAuthenticated.Should().BeTrue();
    }

    /// <summary>
    /// Handlers scope every "my" read and write to this value; answering something falsy instead of
    /// throwing would scope them to nobody, which reads as an empty account rather than a refusal.
    /// </summary>
    [Fact]
    public void RequireUserId_WithNoSubject_Refuses()
    {
        var service = Build(new FakeIdentityProvider(Ada));

        service.Invoking(s => s.RequireUserId()).Should().Throw<UnauthorizedAccessException>();
    }

    /// <summary>A claim saying true is true.</summary>
    [Fact]
    public void IsEmailVerified_WhenTheClaimSaysTrue_IsTrue()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim("email_verified", "true"));

        service.IsEmailVerified.Should().BeTrue();
    }

    /// <summary>
    /// Silence is not a "no": the claim only travels when the OIDC <c>email</c> scope was granted, so a
    /// token without it answers null and the caller decides what not knowing means.
    /// </summary>
    [Fact]
    public void IsEmailVerified_WhenTheClaimIsAbsent_IsNull()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim("sub", Subject));

        service.IsEmailVerified.Should().BeNull();
    }

    /// <summary>A claim saying false is false.</summary>
    [Fact]
    public void IsEmailVerified_WhenTheClaimSaysFalse_IsFalse()
    {
        var service = Build(new FakeIdentityProvider(Ada), new Claim("email_verified", "false"));

        service.IsEmailVerified.Should().BeFalse();
    }
}
