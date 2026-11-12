namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>The signals the product runs, listed once. A new signal is a new class and one line here.</summary>
public static class FraudSignals
{
    /// <summary>Every signal, in the order their reasons are reported.</summary>
    public static readonly IReadOnlyList<IFraudSignal> All =
    [
        new BlockedIpSignal(),
        new BlockedEmailDomainSignal(),
        new BlockedCountrySignal(),
        new DisposableEmailSignal(),
        new SharedIpAccountsSignal(),
        new VelocitySignal(),
        new CountryMismatchSignal(),
    ];
}
