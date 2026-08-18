namespace Innovayse.Integration.Tests.Auth;

using FluentAssertions;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for the local identity provider, against a real PostgreSQL container.
///
/// <para>
/// These are the contract with deployments that have no SSO: the behaviour here is what
/// this product has always done, and it has to keep doing it after identity moves behind
/// an interface.
/// </para>
/// </summary>
public sealed class LocalIdentityProviderTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    private static async Task<AppUser> CreateUserAsync(
        IServiceProvider services, string email, string first, string last)
    {
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = first,
            LastName = last,
        };

        var result = await users.CreateAsync(user, "Str0ng!Passw0rd");
        result.Succeeded.Should().BeTrue(
            "the test needs a real user: {0}",
            string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    /// <summary>A user is findable by the subject the provider gave them.</summary>
    [Fact]
    public async Task FindBySubjectAsync_ReturnsTheUserAsync()
    {
        using var scope = factory.Services.CreateScope();
        var email = $"subject-{Guid.NewGuid():N}@example.com";
        var created = await CreateUserAsync(scope.ServiceProvider, email, "Ada", "Lovelace");

        var found = await scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>()
            .FindBySubjectAsync(created.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Subject.Should().Be(created.Id);
        found.Email.Should().Be(email);
        found.FirstName.Should().Be("Ada");
        found.LastName.Should().Be("Lovelace");
    }

    /// <summary>
    /// Lookup by email ignores case. People type their address the way they feel like it,
    /// and Identity stores a normalised copy precisely so that this works.
    /// </summary>
    [Fact]
    public async Task FindByEmailAsync_IgnoresCaseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var email = $"Mixed-{Guid.NewGuid():N}@Example.COM";
        var created = await CreateUserAsync(scope.ServiceProvider, email, "Grace", "Hopper");

        var found = await scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>()
            .FindByEmailAsync(email.ToLowerInvariant(), CancellationToken.None);

        found.Should().NotBeNull();
        found!.Subject.Should().Be(created.Id);
    }

    /// <summary>An address nobody holds resolves to nothing rather than to someone else.</summary>
    [Fact]
    public async Task FindByEmailAsync_ForAnUnknownAddress_ReturnsNullAsync()
    {
        using var scope = factory.Services.CreateScope();

        var found = await scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>()
            .FindByEmailAsync($"nobody-{Guid.NewGuid():N}@example.com", CancellationToken.None);

        found.Should().BeNull();
    }

    /// <summary>
    /// The bulk lookup answers for the subjects it knows and stays silent about the rest.
    /// The admin screens feed it whatever subjects their client rows hold, including the
    /// legacy ones that resolve to nobody.
    /// </summary>
    [Fact]
    public async Task GetEmailsBySubjectsAsync_AnswersOnlyForKnownSubjectsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var one = await CreateUserAsync(
            scope.ServiceProvider, $"bulk1-{Guid.NewGuid():N}@example.com", "Alan", "Turing");
        var two = await CreateUserAsync(
            scope.ServiceProvider, $"bulk2-{Guid.NewGuid():N}@example.com", "Edsger", "Dijkstra");
        var unknown = $"not-a-subject-{Guid.NewGuid():N}";

        var emails = await provider.GetEmailsBySubjectsAsync(
            [one.Id, two.Id, unknown], CancellationToken.None);

        emails.Should().HaveCount(2);
        emails[one.Id].Should().Be(one.Email);
        emails[two.Id].Should().Be(two.Email);
        emails.ContainsKey(unknown).Should().BeFalse();
    }

    /// <summary>An empty request costs no query and returns nothing.</summary>
    [Fact]
    public async Task GetEmailsBySubjectsAsync_ForNoSubjects_ReturnsEmptyAsync()
    {
        using var scope = factory.Services.CreateScope();

        var emails = await scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>()
            .GetEmailsBySubjectsAsync([], CancellationToken.None);

        emails.Should().BeEmpty();
    }

    /// <summary>
    /// Paging reports the unpaged total and never repeats a row between pages. Without a
    /// stable order the same row can appear on two pages and another on none, which reads
    /// as data going missing.
    /// </summary>
    [Fact]
    public async Task ListAsync_PagesWithoutRepeatingOrLosingRowsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 5; i++)
        {
            await CreateUserAsync(
                scope.ServiceProvider, $"page-{marker}-{i}@example.com", $"Page{i}", marker);
        }

        var (first, total) = await provider.ListAsync(marker, page: 1, pageSize: 2, CancellationToken.None);
        var (second, _) = await provider.ListAsync(marker, page: 2, pageSize: 2, CancellationToken.None);

        total.Should().Be(5);
        first.Should().HaveCount(2);
        second.Should().HaveCount(2);
        first.Select(x => x.Subject).Should().NotIntersectWith(second.Select(x => x.Subject));
    }

    /// <summary>Search matches on name as well as address.</summary>
    [Fact]
    public async Task ListAsync_SearchesByNameAsync()
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var surname = $"Sur{Guid.NewGuid():N}"[..12];
        await CreateUserAsync(
            scope.ServiceProvider, $"named-{Guid.NewGuid():N}@example.com", "Katherine", surname);

        var (items, total) = await provider.ListAsync(
            surname.ToLowerInvariant(), page: 1, pageSize: 10, CancellationToken.None);

        total.Should().Be(1);
        items.Should().ContainSingle().Which.LastName.Should().Be(surname);
    }
}
