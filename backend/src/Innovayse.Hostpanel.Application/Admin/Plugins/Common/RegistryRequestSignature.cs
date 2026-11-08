namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>The four values a signed registry request carries in its <c>X-Hostpanel-*</c> headers.</summary>
/// <param name="InstanceId">The id the registry gave this installation.</param>
/// <param name="Timestamp">The signing moment in unix seconds, as the header text.</param>
/// <param name="Nonce">A random value used for this one request.</param>
/// <param name="Signature">The base64 Ed25519 signature over the canonical request text.</param>
public sealed record RegistryRequestSignature(Guid InstanceId, string Timestamp, string Nonce, string Signature);
