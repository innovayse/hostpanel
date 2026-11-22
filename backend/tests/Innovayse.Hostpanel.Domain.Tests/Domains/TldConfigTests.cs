using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Domain.Tests.Domains;

/// <summary>Unit tests for the <see cref="TldConfig"/> aggregate.</summary>
public sealed class TldConfigTests
{
    /// <summary>Builds a TLD configuration the tests act on.</summary>
    /// <returns>A new configuration for <c>.com</c>.</returns>
    private static TldConfig NewConfig() =>
        TldConfig.Create(" .COM ", "registrar-one", "AMD", "USD", true, 1, ["popular"]);

    /// <summary>Create trims and lower-cases the extension and keeps the other fields.</summary>
    [Fact]
    public void Create_NormalisesTheExtension()
    {
        var config = NewConfig();

        config.Tld.Should().Be(".com");
        config.Currency.Should().Be("AMD");
        config.SellCurrency.Should().Be("USD");
        config.IsEnabled.Should().BeTrue();
        config.Categories.Should().Equal("popular");
        config.LastSyncedAt.Should().BeNull();
    }

    /// <summary>Create keeps the registrar plugin id without the spaces around it, and a blank id is refused.</summary>
    [Fact]
    public void Create_TrimsTheRegistrarModuleAndRefusesABlankOne()
    {
        var config = TldConfig.Create("com", " registrar-one ", "AMD", "USD", true, 1, []);

        config.RegistrarModule.Should().Be("registrar-one");
        FluentActions.Invoking(() => TldConfig.Create("com", " ", "AMD", "USD", true, 1, []))
            .Should().Throw<ArgumentException>();
    }

    /// <summary>Update replaces the registrar module with the trimmed id, and a blank id is refused.</summary>
    [Fact]
    public void Update_ReplacesTheRegistrarModuleAndRefusesABlankOne()
    {
        var config = NewConfig();

        config.Update("registrar-two ", "AMD", "USD", true, 1, []);

        config.RegistrarModule.Should().Be("registrar-two");
        FluentActions.Invoking(() => config.Update("", "AMD", "USD", true, 1, []))
            .Should().Throw<ArgumentException>();
    }

    /// <summary>UpdateSellPrices replaces the three sell price tables.</summary>
    [Fact]
    public void UpdateSellPrices_ReplacesSellTables()
    {
        var config = NewConfig();

        config.UpdateSellPrices(
            new Dictionary<int, decimal> { [1] = 10m },
            new Dictionary<int, decimal> { [1] = 11m },
            new Dictionary<int, decimal> { [1] = 12m });

        config.SellRegister[1].Should().Be(10m);
        config.SellTransfer[1].Should().Be(11m);
        config.SellRenew[1].Should().Be(12m);
    }

    /// <summary>UpdateCostPrices replaces the three cost tables and copies the input.</summary>
    [Fact]
    public void UpdateCostPrices_ReplacesCostTablesWithCopies()
    {
        var config = NewConfig();
        var register = new Dictionary<int, decimal> { [1] = 5m };

        config.UpdateCostPrices(
            register,
            new Dictionary<int, decimal> { [1] = 6m },
            new Dictionary<int, decimal> { [1] = 7m });
        register[1] = 99m;

        config.CostRegister[1].Should().Be(5m);
        config.CostTransfer[1].Should().Be(6m);
        config.CostRenew[1].Should().Be(7m);
    }

    /// <summary>MarkSynced stamps the time of the last successful sync.</summary>
    [Fact]
    public void MarkSynced_StampsLastSyncedAt()
    {
        var config = NewConfig();
        var syncedAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        config.MarkSynced(syncedAt);

        config.LastSyncedAt.Should().Be(syncedAt);
    }
}
