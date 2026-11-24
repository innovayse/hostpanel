namespace Innovayse.Hostpanel.Domain.Services.Enums;

/// <summary>Specifies where a free-trial service is in its trial.</summary>
public enum TrialStatus
{
    /// <summary>The trial is running.</summary>
    Active = 0,

    /// <summary>The trial has been converted and its first invoice waits to be paid.</summary>
    AwaitingPayment = 1,

    /// <summary>The first invoice was paid: the service is an ordinary paid service.</summary>
    ConvertedToPaid = 2,

    /// <summary>The trial ended without payment and the service was terminated.</summary>
    Terminated = 3,
}
