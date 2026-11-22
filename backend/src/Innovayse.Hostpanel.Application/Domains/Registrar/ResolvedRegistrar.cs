using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Domains.Registrar;

/// <summary>
/// The registrar a TLD resolves to: the provider to call and the module that names it (the id of the registrar plugin), so a new
/// domain can store the module it was placed at.
/// </summary>
/// <param name="Provider">The registrar provider.</param>
/// <param name="Module">The module the provider belongs to; null when the name's TLD has no configuration and the provider refuses every operation.</param>
public sealed record ResolvedRegistrar(IRegistrarProvider Provider, string? Module);
