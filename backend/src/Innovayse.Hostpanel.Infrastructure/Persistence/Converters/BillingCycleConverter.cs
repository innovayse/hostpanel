using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Innovayse.Hostpanel.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores a <see cref="BillingCycle"/> in <c>ClientServices.BillingCycle</c> and
/// <c>OrderItems.BillingCycle</c> as the member name, and reads every spelling older rows carry.
/// </summary>
/// <remarks>
/// Rows written before the cycle was an enum hold "monthly", "annual" or "annually" in any casing.
/// They all read back as the matching member; the data step of the release rewrites them to the
/// member name, which is also what every new row gets. The members added since (quarterly, semi-annual
/// and biennial) are stored and read by their name too. A text that names no cycle is a data fault and
/// fails the read loudly: guessing a cycle would bill the client for the wrong amount of time.
/// </remarks>
public sealed class BillingCycleConverter : ValueConverter<BillingCycle, string>
{
    /// <summary>Initialises the converter.</summary>
    public BillingCycleConverter()
        : base(cycle => ToStored(cycle), value => FromStored(value))
    {
    }

    /// <summary>Gets the stored form of a cycle.</summary>
    /// <param name="cycle">The billing cycle to store.</param>
    /// <returns>The column value: the member name.</returns>
    public static string ToStored(BillingCycle cycle) => cycle.ToString();

    /// <summary>Reads a stored value back.</summary>
    /// <param name="value">The column value, in any spelling the platform has written.</param>
    /// <returns>The cycle the text names.</returns>
    /// <exception cref="InvalidOperationException">The text names no billing cycle.</exception>
    public static BillingCycle FromStored(string value) =>
        BillingCycleParser.TryParse(value, out var cycle)
            ? cycle
            : throw new InvalidOperationException($"'{value}' is not a billing cycle.");
}
