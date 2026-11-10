using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Seeding;

/// <summary>
/// Seeds the standard support departments on a fresh install.
/// <para>
/// Without these a production install would come up with an empty table and the ticket
/// form would have nothing to offer in its department picker. Like the role seeding
/// in <c>Program.cs</c>, this is data the app cannot function without, so it runs in
/// every environment.
/// </para>
/// </summary>
public static class DefaultDepartmentsSeeder
{
    /// <summary>Builds the departments created on a fresh install. Editable afterwards in the admin UI.</summary>
    /// <param name="options">The configured contact addresses.</param>
    /// <returns>Name and contact address per department.</returns>
    private static DefaultDepartment[] DefaultsFor(DefaultDepartmentsOptions options) =>
    [
        new("Technical Support", options.SupportEmail),
        new("Billing", options.BillingEmail),
        new("Sales", options.SalesEmail),
        new("General", options.GeneralEmail),
    ];

    /// <summary>
    /// Creates the default departments if none exist yet. Idempotent, and deliberately
    /// keyed on the table being empty rather than on individual names: an admin who has
    /// renamed or deleted a department should not have it silently reappear on restart.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="options">The configured contact addresses for the new departments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The number of departments created; zero when the table was not empty or a concurrent start
    /// inserted them first.
    /// </returns>
    public static async Task<int> EnsureSeededAsync(
        IHostpanelDbContext context,
        DefaultDepartmentsOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var hasAny = await context.Departments.AsNoTracking().AnyAsync(ct);
        if (hasAny)
        {
            return 0;
        }

        var defaults = DefaultsFor(options);
        foreach (var (name, email) in defaults)
        {
            context.Departments.Add(Department.Create(name, email));
        }

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index on the name: two instances starting together both saw an empty table
            // and the other one inserted first. Our rejected rows are dropped from the tracker, and
            // the start carries on when the departments are there; anything else is a real failure.
            context.DetachAll();
            if (!await context.Departments.AsNoTracking().AnyAsync(ct))
            {
                throw;
            }

            return 0;
        }

        return defaults.Length;
    }
}
