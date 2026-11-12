namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The wire values of the consent wording versions the portal publishes, written out literally so a test pins the
/// contract with the portal's own enum (<c>client/types/consenttextversion.ts</c>) and does not follow the server's
/// mapping wherever that moves.
/// </summary>
public static class ConsentTextVersionWire
{
    /// <summary>The consent line on the account's saved-methods card.</summary>
    public const string AccountCard = "account-card-2026-11";

    /// <summary>The agreement under the checkout box, which describes saving the method within the payment itself.</summary>
    public const string CheckoutAgreement = "checkout-agreement-2026-11-b";
}
