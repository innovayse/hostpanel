using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Commands.AdjustClientCredit;

/// <summary>Command for staff to add credit to a client's balance or take credit off it, with a reason.</summary>
/// <param name="ClientId">The client; the route's id wins over any value in the body.</param>
/// <param name="Direction">Whether credit is added or taken.</param>
/// <param name="Amount">The positive amount, in the currency the balance is held in.</param>
/// <param name="Reason">Why the balance is adjusted; kept on the credit history line.</param>
public sealed record AdjustClientCreditCommand(int ClientId, CreditAdjustment Direction, decimal Amount, string Reason);
