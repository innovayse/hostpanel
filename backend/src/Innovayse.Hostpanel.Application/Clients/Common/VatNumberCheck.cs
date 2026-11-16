using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>What <c>IVatNumberChecker</c> answers.</summary>
/// <param name="Outcome">Whether the number is registered, is not, or could not be judged.</param>
/// <param name="Name">The registered name the service returned, or null when it returned none or the number is not valid.</param>
/// <param name="Address">The registered address the service returned, or null when it returned none or the number is not valid.</param>
public sealed record VatNumberCheck(VatCheckOutcome Outcome, string? Name = null, string? Address = null);
