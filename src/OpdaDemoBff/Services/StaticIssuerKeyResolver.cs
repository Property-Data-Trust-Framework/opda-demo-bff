using System.Security.Cryptography;
using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Logging;

namespace OpdaDemoBff.Services;

// Sandbox-grade trust registry: a JSON map of issuer (the `iss` HTTPS URL,
// per Attachment A) to PEM-encoded RSA public key, loaded once at startup from
// the SSM parameter WALLET_TRUSTED_ISSUERS_PATH. This is deliberately the same
// shape as ADR-0012's auth-stub client registry (one SSM JSON parameter, no
// real PKI) — swap this class for one backed by OpenID Federation / a real
// trust registry once one exists; nothing else in the wallet pipeline needs to
// change, since callers only see IIssuerKeyResolver.
public sealed class StaticIssuerKeyResolver : IIssuerKeyResolver
{
    private readonly IReadOnlyDictionary<string, string> _issuerToPem;

    public StaticIssuerKeyResolver(string? trustedIssuersJson)
    {
        _issuerToPem = string.IsNullOrWhiteSpace(trustedIssuersJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(trustedIssuersJson)
              ?? new Dictionary<string, string>();
    }

    // WALLET_TRUSTED_ISSUERS_PATH is injected by Terraform; absent in tests and
    // absent until an operator actually registers a trusted issuer, in which
    // case every credential is parsed but nothing verifies — same fail-closed
    // behaviour as an unset SSM_TRANSPORT_CERTIFICATE_NAME on the OAuth side.
    public static async Task<StaticIssuerKeyResolver> CreateAsync(string? ssmPath, ILogger log)
    {
        if (string.IsNullOrEmpty(ssmPath))
        {
            log.LogWarning("WALLET_TRUSTED_ISSUERS_PATH not set — no VC issuer is trusted, every presentation will fail verification");
            return new StaticIssuerKeyResolver(null);
        }

        using var ssm = new AmazonSimpleSystemsManagementClient();
        var param = await ssm.GetParameterAsync(new GetParameterRequest { Name = ssmPath, WithDecryption = true });
        return new StaticIssuerKeyResolver(param.Parameter.Value);
    }

    public Task<RSA?> ResolveAsync(string issuer, CancellationToken ct = default)
    {
        if (!_issuerToPem.TryGetValue(issuer, out var pem))
            return Task.FromResult<RSA?>(null);

        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return Task.FromResult<RSA?>(rsa);
    }
}
