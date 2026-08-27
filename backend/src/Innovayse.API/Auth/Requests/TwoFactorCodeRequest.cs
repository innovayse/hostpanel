namespace Innovayse.API.Auth.Requests;

/// <summary>HTTP request body carrying a six-digit TOTP code.</summary>
/// <param name="Code">The digits the authenticator app currently shows.</param>
public record TwoFactorCodeRequest(string Code);
