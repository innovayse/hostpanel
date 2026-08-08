namespace Innovayse.Application.Support.Services;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Seeds the standard support departments on a fresh install.
/// <para>
/// Departments were previously created only by <c>DevDataSeeder</c>, which runs in
/// Development alone — so a production install came up with an empty table and the
/// ticket form had nothing to offer in its department picker. Like the role seeding
/// in <c>Program.cs</c>, this is data the app cannot function without, so it runs in
/// every environment.
/// </para>
/// </summary>
public static class DefaultDepartmentsSeeder
{
    /// <summary>The departments created on a fresh install. Editable afterwards in the admin UI.</summary>
    private static readonly (string Name, string Email)[] Defaults =
    [
        ("Technical Support", "support@innovayse.com"),
        ("Billing",           "billing@innovayse.com"),
        ("Sales",             "sales@innovayse.com"),
        ("General",           "hello@innovayse.com"),
    ];

    /// <summary>
    /// Creates the default departments if none exist yet. Idempotent, and deliberately
    /// keyed on the table being empty rather than on individual names: an admin who has
    /// renamed or deleted a department should not have it silently reappear on restart.
    /// </summary>
    /// <param name="departments">Department repository.</param>
    /// <param name="uow">Unit of work for persisting the new departments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of departments created.</returns>
    public static async Task<int> EnsureSeededAsync(
        IDepartmentRepository departments,
        IUnitOfWork uow,
        CancellationToken ct = default)
    {
        var existing = await departments.ListAllAsync(ct);
        if (existing.Count > 0)
        {
            return 0;
        }

        foreach (var (name, email) in Defaults)
        {
            departments.Add(Department.Create(name, email));
        }

        await uow.SaveChangesAsync(ct);
        return Defaults.Length;
    }
}
