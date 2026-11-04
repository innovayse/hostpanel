namespace Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;

/// <summary>
/// Internal: reads several settings by key in one round trip, for a module that needs a group of
/// settings read together. Dispatched over the bus, never routed. Only keys of the general
/// settings catalogue are read; any other key is absent from the answer.
/// </summary>
/// <param name="Keys">The setting keys to read; a key with no row is absent from the answer.</param>
public sealed record GetSettingValuesQuery(IReadOnlyList<string> Keys);
