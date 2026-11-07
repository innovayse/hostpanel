namespace Innovayse.Hostpanel.Application.Common.Enums;

/// <summary>How far ahead the upcoming renewals pages look; the value is the number of days.</summary>
public enum RenewalWindow
{
    /// <summary>The next 30 days.</summary>
    Next30Days = 30,

    /// <summary>The next 60 days.</summary>
    Next60Days = 60,

    /// <summary>The next 90 days.</summary>
    Next90Days = 90,
}
