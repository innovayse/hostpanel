namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>One movement of a client's credit balance in the Credits Reviewer report.</summary>
/// <param name="Id">The ledger row identifier.</param>
/// <param name="ClientId">The client credited.</param>
/// <param name="ClientName">The client's full name.</param>
/// <param name="Date">The day of the credit (UTC), <c>yyyy-MM-dd</c>.</param>
/// <param name="Description">The ledger row's description.</param>
/// <param name="Currency">The ISO 4217 code <paramref name="Amount"/> is in.</param>
/// <param name="Amount">The signed change to the balance (negative when credit was taken), or the cash put on the balance for an older row.</param>
public sealed record CreditsReviewerRowDto(
    int Id,
    int ClientId,
    string ClientName,
    string Date,
    string Description,
    string Currency,
    decimal Amount);
