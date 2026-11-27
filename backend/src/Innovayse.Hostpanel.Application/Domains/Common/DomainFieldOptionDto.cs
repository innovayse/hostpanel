namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>One choice of a select domain field.</summary>
/// <param name="Value">The value stored and sent to the registrar plugin when the choice is picked.</param>
/// <param name="Label">The text shown for the choice, in the caller's language.</param>
public sealed record DomainFieldOptionDto(string Value, string Label);
