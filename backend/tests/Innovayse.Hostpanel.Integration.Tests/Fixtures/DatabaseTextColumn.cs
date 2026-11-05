namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>One database column whose values can hold text, as the leak scan of <see cref="PluginSshFixture"/> reads it.</summary>
/// <param name="Schema">The schema: <c>public</c> for the application tables, <c>wolverine</c> for the message store.</param>
/// <param name="Table">The table name, as the catalogue spells it.</param>
/// <param name="Column">The column name, as the catalogue spells it.</param>
/// <param name="IsBinary">Whether the column is <c>bytea</c>, searched for the needle's UTF-8 bytes rather than as text.</param>
public sealed record DatabaseTextColumn(string Schema, string Table, string Column, bool IsBinary);
