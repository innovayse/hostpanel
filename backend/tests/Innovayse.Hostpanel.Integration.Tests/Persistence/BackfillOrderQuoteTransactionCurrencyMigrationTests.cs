namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The <c>BackfillOrderQuoteTransactionCurrency</c> migration fills the <c>Currency</c> column the earlier
/// migration added with an empty string on every existing order, quote and transaction. These tests build a
/// database at the schema before the backfill, seed rows with an empty currency (and rows that already carry
/// one), apply the migration, and prove each row got the right currency and no stamped row was touched.
/// </summary>
public sealed class BackfillOrderQuoteTransactionCurrencyMigrationTests
{
    /// <summary>The base currency the <c>AddCurrencies</c> migration seeds.</summary>
    internal const string BaseCurrency = "USD";

    /// <summary>The currency of the client that has one.</summary>
    internal const string ClientCurrency = "EUR";

    /// <summary>The currency of the invoice, different from the client's so the precedence is visible.</summary>
    internal const string InvoiceCurrency = "GBP";

    /// <summary>A currency stamped before the backfill ran, which must survive it.</summary>
    internal const string StampedCurrency = "AMD";

    /// <summary>
    /// Transactions take the linked invoice's currency, else the client's, else the base currency; a transaction
    /// that already has a currency keeps it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Up_OnTransactionsWithEmptyCurrency_TakesInvoiceThenClientThenBaseAsync()
    {
        await using var database = await SeededDatabase.CreateAsync();

        await database.MigrateToBackfillAsync();

        (await database.CurrencyOfAsync("Transactions", database.TransactionOnInvoice)).Should().Be(InvoiceCurrency, "the invoice wins over the client");
        (await database.CurrencyOfAsync("Transactions", database.TransactionOfCurrencyClient)).Should().Be(ClientCurrency, "no invoice, so the client's currency");
        (await database.CurrencyOfAsync("Transactions", database.TransactionOfBareClient)).Should().Be(BaseCurrency, "no invoice and no client currency, so the base");
        (await database.CurrencyOfAsync("Transactions", database.TransactionStamped)).Should().Be(StampedCurrency, "a stamped row is not rewritten");
    }

    /// <summary>
    /// Orders take the linked invoice's currency, else the client's, else the base currency; an order that already
    /// has a currency keeps it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Up_OnOrdersWithEmptyCurrency_TakesInvoiceThenClientThenBaseAsync()
    {
        await using var database = await SeededDatabase.CreateAsync();

        await database.MigrateToBackfillAsync();

        (await database.CurrencyOfAsync("Orders", database.OrderOnInvoice)).Should().Be(InvoiceCurrency, "the invoice wins over the client");
        (await database.CurrencyOfAsync("Orders", database.OrderOfCurrencyClient)).Should().Be(ClientCurrency, "no invoice, so the client's currency");
        (await database.CurrencyOfAsync("Orders", database.OrderOfBareClient)).Should().Be(BaseCurrency, "no invoice and no client currency, so the base");
        (await database.CurrencyOfAsync("Orders", database.OrderStamped)).Should().Be(StampedCurrency, "a stamped row is not rewritten");
    }

    /// <summary>Quotes take the client's currency, else the base currency; a quote that already has a currency keeps it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Up_OnQuotesWithEmptyCurrency_TakesClientThenBaseAsync()
    {
        await using var database = await SeededDatabase.CreateAsync();

        await database.MigrateToBackfillAsync();

        (await database.CurrencyOfAsync("Quotes", database.QuoteOfCurrencyClient)).Should().Be(ClientCurrency, "the client's currency");
        (await database.CurrencyOfAsync("Quotes", database.QuoteOfBareClient)).Should().Be(BaseCurrency, "no client currency, so the base");
        (await database.CurrencyOfAsync("Quotes", database.QuoteStamped)).Should().Be(StampedCurrency, "a stamped row is not rewritten");
    }

    /// <summary>The backfill cannot be undone, so going down keeps the data and the filled currencies.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Down_AfterTheBackfill_KeepsTheFilledCurrenciesAsync()
    {
        await using var database = await SeededDatabase.CreateAsync();
        await database.MigrateToBackfillAsync();

        await database.MigrateDownAsync();

        (await database.CurrencyOfAsync("Orders", database.OrderOnInvoice)).Should().Be(InvoiceCurrency);
        (await database.CurrencyOfAsync("Quotes", database.QuoteOfBareClient)).Should().Be(BaseCurrency);
        (await database.CurrencyOfAsync("Transactions", database.TransactionOfCurrencyClient)).Should().Be(ClientCurrency);
    }
}
