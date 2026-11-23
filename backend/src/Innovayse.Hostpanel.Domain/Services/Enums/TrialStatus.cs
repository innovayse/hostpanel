namespace Innovayse.Hostpanel.Domain.Services.Enums;

/// <summary>Specifies where a free-trial service is in its trial.</summary>
public enum TrialStatus
{
    /// <summary>The trial is running.</summary>
    Active = 0,

    /// <summary>The trial ended and turned into a paid service whose first invoice is not paid yet.</summary>
    AwaitingPayment = 1,

    /// <summary>The trial turned into a paid service and the first invoice was paid.</summary>
    ConvertedToPaid = 2,

    /// <summary>The trial ended without becoming a paid service.</summary>
    Terminated = 3,
}
