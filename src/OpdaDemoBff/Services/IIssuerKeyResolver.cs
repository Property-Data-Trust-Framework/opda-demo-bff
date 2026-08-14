using System.Security.Cryptography;

namespace OpdaDemoBff.Services;

// Resolves a VC issuer's RS256 public key so a presented credential's signature
// can actually be checked, not just parsed. Deliberately separate from
// SdJwtVc — structural parsing must never be mistaken for trust.
public interface IIssuerKeyResolver
{
    Task<RSA?> ResolveAsync(string issuer, CancellationToken ct = default);
}
