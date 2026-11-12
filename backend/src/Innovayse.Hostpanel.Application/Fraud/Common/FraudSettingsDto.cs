namespace Innovayse.Hostpanel.Application.Fraud.Common;

/// <summary>The operator's fraud detection settings.</summary>
/// <param name="Enabled">Whether registrations and orders are checked at all.</param>
/// <param name="ManualReviewRiskScore">The risk score (0 to 100) from which an order is held for staff review.</param>
/// <param name="AutoRejectRiskScore">The risk score (0 to 100) from which a request is refused; always above <paramref name="ManualReviewRiskScore"/>.</param>
/// <param name="VelocityWindowMinutes">The window, in minutes, that orders from one IP address are counted over.</param>
/// <param name="VelocityMaxOrders">How many orders one IP address may place within the window before the next one scores risk.</param>
/// <param name="MaxAccountsPerIp">How many accounts one IP address may carry before the next one scores risk.</param>
public sealed record FraudSettingsDto(
    bool Enabled,
    int ManualReviewRiskScore,
    int AutoRejectRiskScore,
    int VelocityWindowMinutes,
    int VelocityMaxOrders,
    int MaxAccountsPerIp);
