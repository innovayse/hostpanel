using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.UpdateFraudSettings;

/// <summary>Saves the fraud detection settings, all of them together so the thresholds can be checked against each other.</summary>
/// <param name="Enabled">Whether registrations and orders are checked at all.</param>
/// <param name="ManualReviewRiskScore">The risk score (0 to 100) from which an order is held for staff review; below <paramref name="AutoRejectRiskScore"/>.</param>
/// <param name="AutoRejectRiskScore">The risk score (0 to 100) from which a request is refused.</param>
/// <param name="VelocityWindowMinutes">The window, in minutes (1 to 1440), that orders from one IP address are counted over.</param>
/// <param name="VelocityMaxOrders">How many orders (1 to 100) one IP address may place within the window before the next one scores risk.</param>
/// <param name="MaxAccountsPerIp">How many accounts (1 to 100) one IP address may carry before the next one scores risk.</param>
public sealed record UpdateFraudSettingsCommand(
    bool Enabled,
    int ManualReviewRiskScore,
    int AutoRejectRiskScore,
    int VelocityWindowMinutes,
    int VelocityMaxOrders,
    int MaxAccountsPerIp) : IDemoRestricted;
