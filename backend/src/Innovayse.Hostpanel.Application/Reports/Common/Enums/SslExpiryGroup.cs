namespace Innovayse.Hostpanel.Application.Reports.Common.Enums;

/// <summary>The expiry buckets of the SSL monitoring report, soonest expiry first and "no SSL" last. Values start at 1.</summary>
public enum SslExpiryGroup
{
    /// <summary>The certificate expires within 30 days, or has already expired.</summary>
    Within30Days = 1,

    /// <summary>The certificate expires in more than 30 and at most 90 days.</summary>
    Within90Days = 2,

    /// <summary>The certificate expires in more than 90 and at most 180 days.</summary>
    Within180Days = 3,

    /// <summary>The certificate expires in more than 180 days.</summary>
    Over180Days = 4,

    /// <summary>No certificate was found, or its expiry is unknown.</summary>
    NoSsl = 5,
}
