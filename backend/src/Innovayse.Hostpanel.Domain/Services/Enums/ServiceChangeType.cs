namespace Innovayse.Hostpanel.Domain.Services.Enums;

/// <summary>Whether a plan change moves a service to a dearer or a cheaper plan.</summary>
public enum ServiceChangeType
{
    /// <summary>The new plan costs more per cycle than the current one; the client pays the prorated difference.</summary>
    Upgrade = 0,

    /// <summary>The new plan costs less per cycle than the current one; the prorated difference goes to the client's balance.</summary>
    Downgrade = 1,
}
