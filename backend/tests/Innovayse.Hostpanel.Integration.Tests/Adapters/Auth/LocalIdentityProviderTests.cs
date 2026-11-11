using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Auth;

/// <summary>
/// Integration tests for the local identity provider, against a real PostgreSQL container.
///
/// <para>
/// These are the contract with deployments that have no external sign-on provider: the behaviour here is what
/// this product has always done, and it has to keep doing it after identity moves behind
/// an interface.
/// </para>
/// </summary>
public sealed class LocalIdentityProviderTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creates a local account the provider can then read back.</summary>
    /// <param name="services">Root provider of the test host.</param>
    /// <param name="email">Address and user name of the new account.</param>
    /// <param name="first">Given name.</param>
    /// <param name="last">Family name.</param>
    /// <returns>The persisted account.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetAccountsBySubjectsAsync_AnswersOnlyForKnownSubjectsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var one = await CreateUserAsync(
            scope.ServiceProvider, $"bulk1-{Guid.NewGuid():N}@example.com", "Alan", "Turing");
        var two = await CreateUserAsync(
            scope.ServiceProvider, $"bulk2-{Guid.NewGuid():N}@example.com", "Edsger", "Dijkstra");
        var unknown = $"not-a-subject-{Guid.NewGuid():N}";

        var accounts = await provider.GetAccountsBySubjectsAsync(
            [one.Id, two.Id, unknown], CancellationToken.None);

        accounts.Should().HaveCount(2);
        accounts[one.Id].Email.Should().Be(one.Email);
        accounts[two.Id].Email.Should().Be(two.Email);
        accounts.ContainsKey(unknown).Should().BeFalse();
    }

    /// <summary>An empty request costs no query and returns nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetAccountsBySubjectsAsync_ForNoSubjects_ReturnsEmptyAsync()
    {
        using var scope = factory.Services.CreateScope();

        var accounts = await scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>()
            .GetAccountsBySubjectsAsync([], CancellationToken.None);

        accounts.Should().BeEmpty();
    }

    /// <summary>
    /// Paging reports the unpaged total and never repeats a row between pages. Without a
    /// stable order the same row can appear on two pages and another on none, which reads
    /// as data going missing.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
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

        var first = await provider.ListAsync(new IdentityAccountListQuery(marker, Page: 1, PageSize: 2), CancellationToken.None);
        var second = await provider.ListAsync(new IdentityAccountListQuery(marker, Page: 2, PageSize: 2), CancellationToken.None);

        first.TotalCount.Should().Be(5);
        first.PageNumber.Should().Be(1);
        first.TotalPages.Should().Be(3);
        first.Items.Should().HaveCount(2);
        second.Items.Should().HaveCount(2);
        first.Items.Select(x => x.Subject).Should().NotIntersectWith(second.Items.Select(x => x.Subject));
    }

    /// <summary>Search matches on name as well as address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListAsync_SearchesByNameAsync()
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var surname = $"Sur{Guid.NewGuid():N}"[..12];
        await CreateUserAsync(
            scope.ServiceProvider, $"named-{Guid.NewGuid():N}@example.com", "Katherine", surname);

        var page = await provider.ListAsync(
            new IdentityAccountListQuery(surname.ToLowerInvariant(), Page: 1, PageSize: 10), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle().Which.LastName.Should().Be(surname);
    }
}
