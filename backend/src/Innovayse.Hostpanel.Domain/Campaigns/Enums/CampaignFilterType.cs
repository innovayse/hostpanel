namespace Innovayse.Hostpanel.Domain.Campaigns.Enums;

/// <summary>The kind of rule a campaign segment uses to pick recipients from the clients who agreed to marketing email.</summary>
public enum CampaignFilterType
{
    /// <summary>Every active client; takes no value.</summary>
    AllActive = 0,

    /// <summary>Clients with an active service of one product; the value is the product id.</summary>
    Product = 1,

    /// <summary>Clients living in one country; the value is the two-letter country code.</summary>
    Country = 2,

    /// <summary>Clients with a service that expires within a number of days; the value is the number of days.</summary>
    ServiceExpiringInDays = 3,

    /// <summary>Clients who belong to one client group; the value is the group id.</summary>
    ClientGroup = 4,

    /// <summary>Clients who have not signed in for a number of days; the value is the number of days.</summary>
    NotSignedInForDays = 5
}
