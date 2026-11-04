namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// The fixed values an importer stores when a source record leaves a required text field blank,
/// kept in one place so the write and the duplicate check always agree on the stored form.
/// </summary>
internal static class MigrationPlaceholders
{
    /// <summary>Stored in place of a blank ticket or reply message.</summary>
    public const string NoMessage = "(no message)";

    /// <summary>Stored in place of a blank reply author name.</summary>
    public const string UnknownAuthor = "(unknown author)";
}
