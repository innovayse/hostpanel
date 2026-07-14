namespace Innovayse.Infrastructure.Email;

using DnsClient;
using DnsClient.Protocol;
using Innovayse.Domain.Email;
using Innovayse.Domain.Email.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Infrastructure implementation of <see cref="IDnsVerifier"/> that performs live DNS lookups
/// using the DnsClient library to verify that required email DNS records are published correctly.
/// </summary>
public sealed class DnsVerificationService(ILogger<DnsVerificationService> logger) : IDnsVerifier
{
    private readonly LookupClient _dns = new(new LookupClientOptions(
        new System.Net.IPEndPoint(System.Net.IPAddress.Parse("8.8.8.8"), 53),
        new System.Net.IPEndPoint(System.Net.IPAddress.Parse("1.1.1.1"), 53))
    {
        UseCache = false,
        Timeout = TimeSpan.FromSeconds(10),
    });

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DnsVerificationResult>> VerifyAsync(
        string domainName,
        IReadOnlyList<DnsRecordRequirement> requirements,
        CancellationToken ct)
    {
        var results = new List<DnsVerificationResult>();

        foreach (var req in requirements)
        {
            var result = req.Type.ToUpperInvariant() switch
            {
                "MX" => await VerifyMxAsync(domainName, req, ct),
                "TXT" => await VerifyTxtAsync(domainName, req, ct),
                "CNAME" => await VerifyCnameAsync(domainName, req, ct),
                _ => new DnsVerificationResult(req.Name, req.Type, false, null, $"Unsupported record type: {req.Type}"),
            };

            results.Add(result);
        }

        return results;
    }

    private async Task<DnsVerificationResult> VerifyMxAsync(string domain, DnsRecordRequirement req, CancellationToken ct)
    {
        try
        {
            var queryName = req.Name == "@" ? domain : $"{req.Name}.{domain}";
            var response = await _dns.QueryAsync(queryName, QueryType.MX, cancellationToken: ct);
            var mxRecords = response.Answers.MxRecords().ToList();

            var found = mxRecords.FirstOrDefault(mx =>
                mx.Exchange.Value.TrimEnd('.').Equals(req.Value.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));

            return new DnsVerificationResult(
                req.Name, "MX", found is not null,
                mxRecords.FirstOrDefault()?.Exchange.Value.TrimEnd('.'),
                found is null ? "MX record not found or does not match" : null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DNS MX lookup failed for {Domain}", domain);
            return new DnsVerificationResult(req.Name, "MX", false, null, ex.Message);
        }
    }

    private async Task<DnsVerificationResult> VerifyTxtAsync(string domain, DnsRecordRequirement req, CancellationToken ct)
    {
        try
        {
            var queryName = req.Name == "@" ? domain : $"{req.Name}.{domain}";
            var response = await _dns.QueryAsync(queryName, QueryType.TXT, cancellationToken: ct);
            var txtRecords = response.Answers.TxtRecords().ToList();

            // For SPF/DMARC/DKIM: check if any TXT record contains the expected value
            // Strip whitespace for comparison — DNS splits long TXT records at 255 chars
            // and some providers insert spaces at the split boundary
            var found = txtRecords.Any(txt =>
            {
                var text = string.Join("", txt.Text).Replace(" ", "");
                var expected = req.Value.Replace(" ", "");
                return text.Contains(expected, StringComparison.OrdinalIgnoreCase)
                    || expected.Contains(text, StringComparison.OrdinalIgnoreCase);
            });

            var foundValue = txtRecords.FirstOrDefault() is { } first
                ? string.Join("", first.Text)
                : null;

            return new DnsVerificationResult(
                req.Name, "TXT", found, foundValue,
                found ? null : "TXT record not found or does not match");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DNS TXT lookup failed for {Name}.{Domain}", req.Name, domain);
            return new DnsVerificationResult(req.Name, "TXT", false, null, ex.Message);
        }
    }

    private async Task<DnsVerificationResult> VerifyCnameAsync(string domain, DnsRecordRequirement req, CancellationToken ct)
    {
        try
        {
            var queryName = $"{req.Name}.{domain}";
            var response = await _dns.QueryAsync(queryName, QueryType.CNAME, cancellationToken: ct);
            var cnameRecords = response.Answers.CnameRecords().ToList();

            var found = cnameRecords.Any(c =>
                c.CanonicalName.Value.TrimEnd('.').Equals(req.Value.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));

            return new DnsVerificationResult(
                req.Name, "CNAME", found,
                cnameRecords.FirstOrDefault()?.CanonicalName.Value.TrimEnd('.'),
                found ? null : "CNAME record not found or does not match");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DNS CNAME lookup failed for {Name}.{Domain}", req.Name, domain);
            return new DnsVerificationResult(req.Name, "CNAME", false, null, ex.Message);
        }
    }
}
