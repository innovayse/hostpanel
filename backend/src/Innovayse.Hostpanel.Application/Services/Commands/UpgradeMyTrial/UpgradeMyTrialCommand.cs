using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Services.Commands.UpgradeMyTrial;

/// <summary>
/// Command for a signed-in client to upgrade one of their own running free trials to a paid service now.
/// </summary>
/// <remarks>
/// Carries a service id but no client id: which account the service must belong to is resolved inside the handler
/// from the credential. <c>ConvertTrialCommand</c>, which this delegates to, names no caller and is what the staff
/// route dispatches.
/// </remarks>
/// <param name="ServiceId">Primary key of the trial service, which must belong to the caller.</param>
public sealed record UpgradeMyTrialCommand(int ServiceId) : ICallerScopedServiceMessage;
