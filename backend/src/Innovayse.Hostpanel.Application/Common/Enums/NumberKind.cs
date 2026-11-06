namespace Innovayse.Hostpanel.Application.Common.Enums;

/// <summary>The kinds of human-readable number the panel hands out, each counted by its own sequence.</summary>
public enum NumberKind
{
    /// <summary>The counter behind <c>ORD-0001</c>-style order numbers.</summary>
    Order,

    /// <summary>The counter behind <c>CN-0001</c>-style credit note numbers.</summary>
    CreditNote,
}
