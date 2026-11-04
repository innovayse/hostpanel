namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>One call a <see cref="StubFulfilmentPlugin"/> received.</summary>
/// <param name="Method">The plugin method called, by its name (<c>OnInvoicePaidAsync</c>, ...).</param>
/// <param name="InvoiceId">The invoice the call carried.</param>
public sealed record FulfilmentCall(string Method, int InvoiceId);
