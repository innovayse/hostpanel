using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Servers;

/// <summary>Unit tests for <see cref="ServerFillPolicy"/>.</summary>
public sealed class ServerFillPolicyTests
{
    /// <summary>The fixed moment every test acts at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The server with the lowest fill ratio wins.</summary>
    [Fact]
    public void SelectBest_PicksTheLeastFilledServer()
    {
        var busy = NewServer("busy", maxAccounts: 10, accounts: 8);
        var quiet = NewServer("quiet", maxAccounts: 10, accounts: 2);

        ServerFillPolicy.SelectBest([busy, quiet], ServerFillType.LeastFull).Should().BeSameAs(quiet);
    }

    /// <summary>On an equal ratio the server with the larger capacity wins.</summary>
    [Fact]
    public void SelectBest_BreaksARatioTieByCapacity()
    {
        var small = NewServer("small", maxAccounts: 10, accounts: 5);
        var large = NewServer("large", maxAccounts: 100, accounts: 50);

        ServerFillPolicy.SelectBest([small, large], ServerFillType.LeastFull).Should().BeSameAs(large);
    }

    /// <summary>A tie on both keys keeps the order the candidates were given in.</summary>
    [Fact]
    public void SelectBest_KeepsTheGivenOrderOnAFullTie()
    {
        var first = NewServer("a", maxAccounts: 10, accounts: 1);
        var second = NewServer("b", maxAccounts: 10, accounts: 1);

        ServerFillPolicy.SelectBest([first, second], ServerFillType.LeastFull).Should().BeSameAs(first);
    }

    /// <summary>Disabled and full servers are never chosen.</summary>
    [Fact]
    public void SelectBest_SkipsDisabledAndFullServers()
    {
        var disabled = NewServer("off", maxAccounts: 10, accounts: 0, isDisabled: true);
        var full = NewServer("full", maxAccounts: 10, accounts: 10);
        var open = NewServer("open", maxAccounts: 10, accounts: 9);

        ServerFillPolicy.SelectBest([disabled, full, open], ServerFillType.LeastFull).Should().BeSameAs(open);
    }

    /// <summary>No eligible candidate gives no server.</summary>
    [Fact]
    public void SelectBest_ReturnsNullWhenNothingQualifies()
    {
        var full = NewServer("full", maxAccounts: 3, accounts: 3);

        ServerFillPolicy.SelectBest([full], ServerFillType.LeastFull).Should().BeNull();
        ServerFillPolicy.SelectBest([], ServerFillType.LeastFull).Should().BeNull();
    }

    /// <summary>A server without a limit counts as empty and is preferred over a partly filled one.</summary>
    [Fact]
    public void SelectBest_TreatsAnUnlimitedServerAsEmpty()
    {
        var limited = NewServer("limited", maxAccounts: 10, accounts: 1);
        var unlimited = NewServer("unlimited", maxAccounts: null, accounts: 500);

        ServerFillPolicy.SelectBest([limited, unlimited], ServerFillType.LeastFull).Should().BeSameAs(unlimited);
        ServerFillPolicy.IsFull(unlimited).Should().BeFalse();
        ServerFillPolicy.FillRatio(unlimited).Should().Be(0.0);
    }

    /// <summary>On an equal ratio and capacity the default server wins under least full.</summary>
    [Fact]
    public void SelectBest_LeastFull_PrefersTheDefaultOnATie()
    {
        var other = NewServer("a", maxAccounts: 10, accounts: 1);
        var preferred = NewServer("b", maxAccounts: 10, accounts: 1, isDefault: true);

        ServerFillPolicy.SelectBest([other, preferred], ServerFillType.LeastFull).Should().BeSameAs(preferred);
    }

    /// <summary>Being the default does not beat a less filled server under least full.</summary>
    [Fact]
    public void SelectBest_LeastFull_DefaultDoesNotBeatALessFilledServer()
    {
        var preferred = NewServer("a", maxAccounts: 10, accounts: 5, isDefault: true);
        var quiet = NewServer("b", maxAccounts: 10, accounts: 1);

        ServerFillPolicy.SelectBest([preferred, quiet], ServerFillType.LeastFull).Should().BeSameAs(quiet);
    }

    /// <summary>Under fill until full the default server comes first even when another is emptier.</summary>
    [Fact]
    public void SelectBest_FillUntilFull_ChoosesTheDefaultFirst()
    {
        var quiet = NewServer("a", maxAccounts: 10, accounts: 0);
        var preferred = NewServer("b", maxAccounts: 10, accounts: 9, isDefault: true);

        ServerFillPolicy.SelectBest([quiet, preferred], ServerFillType.FillUntilFull).Should().BeSameAs(preferred);
    }

    /// <summary>Under fill until full the first server by name is filled, not the emptiest.</summary>
    [Fact]
    public void SelectBest_FillUntilFull_ChoosesTheFirstByNameNotTheEmptiest()
    {
        var second = NewServer("b", maxAccounts: 10, accounts: 0);
        var first = NewServer("a", maxAccounts: 10, accounts: 8);

        ServerFillPolicy.SelectBest([second, first], ServerFillType.FillUntilFull).Should().BeSameAs(first);
    }

    /// <summary>Under fill until full the next server is used once the first is full.</summary>
    [Fact]
    public void SelectBest_FillUntilFull_MovesOnWhenTheFirstIsFull()
    {
        var full = NewServer("a", maxAccounts: 10, accounts: 10);
        var next = NewServer("b", maxAccounts: 10, accounts: 0);

        ServerFillPolicy.SelectBest([full, next], ServerFillType.FillUntilFull).Should().BeSameAs(next);
    }

    /// <summary>Under fill until full a limited server comes before an unlimited one.</summary>
    [Fact]
    public void SelectBest_FillUntilFull_PutsUnlimitedServersAfterLimitedOnes()
    {
        var unlimited = NewServer("a", maxAccounts: null, accounts: 0);
        var limited = NewServer("b", maxAccounts: 10, accounts: 5);

        ServerFillPolicy.SelectBest([unlimited, limited], ServerFillType.FillUntilFull).Should().BeSameAs(limited);
    }

    /// <summary>A disabled or full default server is never chosen, under either fill type.</summary>
    /// <param name="fillType">The fill type under test.</param>
    [Theory]
    [InlineData(ServerFillType.LeastFull)]
    [InlineData(ServerFillType.FillUntilFull)]
    public void SelectBest_NeverChoosesADisabledOrFullDefault(ServerFillType fillType)
    {
        var disabled = NewServer("a", maxAccounts: 10, accounts: 0, isDisabled: true, isDefault: true);
        var full = NewServer("b", maxAccounts: 10, accounts: 10, isDefault: true);
        var open = NewServer("c", maxAccounts: 10, accounts: 5);

        ServerFillPolicy.SelectBest([disabled, full, open], fillType).Should().BeSameAs(open);
    }

    /// <summary>A server whose limit is 0 is full and never chosen, under either fill type.</summary>
    /// <param name="fillType">The fill type under test.</param>
    [Theory]
    [InlineData(ServerFillType.LeastFull)]
    [InlineData(ServerFillType.FillUntilFull)]
    public void SelectBest_NeverChoosesAServerWithAZeroLimit(ServerFillType fillType)
    {
        var zero = NewServer("a", maxAccounts: 0, accounts: 0, isDefault: true);

        ServerFillPolicy.SelectBest([zero], fillType).Should().BeNull();
    }

    /// <summary>An unknown account count counts as zero accounts.</summary>
    [Fact]
    public void FillRatio_TreatsAnUnknownCountAsEmpty()
    {
        var unknown = NewServer("unknown", maxAccounts: 10, accounts: null);

        ServerFillPolicy.FillRatio(unknown).Should().Be(0.0);
        ServerFillPolicy.IsFull(unknown).Should().BeFalse();
    }

    /// <summary>The ratio is accounts over the limit.</summary>
    [Fact]
    public void FillRatio_DividesAccountsByTheLimit()
    {
        var server = NewServer("half", maxAccounts: 4, accounts: 1);

        ServerFillPolicy.FillRatio(server).Should().Be(0.25);
    }

    /// <summary>Builds a server with the given capacity and account count.</summary>
    /// <param name="name">Server name.</param>
    /// <param name="maxAccounts">Account limit, or null for none.</param>
    /// <param name="accounts">Accounts reported by the last connection test, or null when unknown.</param>
    /// <param name="isDisabled">Whether the server is disabled.</param>
    /// <param name="isDefault">Whether the server is the default.</param>
    /// <returns>The server.</returns>
    private static Server NewServer(string name, int? maxAccounts, int? accounts, bool isDisabled = false, bool isDefault = false)
    {
        var server = Server.Create(new ServerDetails(
            Name: name,
            Hostname: $"{name}.example.test",
            IpAddress: null,
            AssignedIpAddresses: null,
            Module: ServerModule.Cwp7,
            Username: "api",
            Credentials: new ServerCredentials(null, null, null),
            UseSSL: true,
            MaxAccounts: maxAccounts,
            IsDefault: isDefault,
            IsDisabled: isDisabled,
            MonthlyCost: 0m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None), Now);
        server.RecordConnectionTest(isOnline: true, accountsCount: accounts, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        return server;
    }
}
