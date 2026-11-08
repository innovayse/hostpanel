namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models.Enums;

/// <summary>What asking WHM about one account established.</summary>
public enum WhmLookupOutcome
{
    /// <summary>The account exists; its details are available.</summary>
    Found = 0,

    /// <summary>WHM answered and positively holds no such account.</summary>
    NotFound = 1,

    /// <summary>
    /// The answer could not be used: a refusal, a transport failure or an unrecognised shape.
    /// Nothing is known about the account, so no caller may read this as "absent".
    /// </summary>
    Unknown = 2,
}
