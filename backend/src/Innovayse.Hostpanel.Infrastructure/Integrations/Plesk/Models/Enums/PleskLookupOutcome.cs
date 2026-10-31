namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models.Enums;

/// <summary>What asking Plesk about one object established.</summary>
public enum PleskLookupOutcome
{
    /// <summary>The object exists; its details are available.</summary>
    Found = 0,

    /// <summary>Plesk answered and positively holds no such object.</summary>
    NotFound = 1,

    /// <summary>
    /// The answer could not be used: a refusal, a transport failure or an unrecognised shape.
    /// Nothing is known about the object, so no caller may read this as "absent".
    /// </summary>
    Unknown = 2,
}
