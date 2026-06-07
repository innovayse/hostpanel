namespace Innovayse.Domain.Slides;

/// <summary>Determines which visitors see a slide.</summary>
public enum SlideAudience
{
    /// <summary>Visible to all visitors.</summary>
    All = 0,

    /// <summary>Visible only to unauthenticated visitors.</summary>
    Guest = 1,

    /// <summary>Visible only to authenticated clients.</summary>
    Authenticated = 2
}
