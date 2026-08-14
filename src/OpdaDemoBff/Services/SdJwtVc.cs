using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

namespace OpdaDemoBff.Services;

// Structural parsing of an SD-JWT VC (draft-ietf-oauth-sd-jwt-vc): the wire format
// used by the mortgage-offer PoC (verifiable_credentials_scope.md, Attachment A).
// This checks the *shape* is internally consistent (every disclosed claim's digest
// really appears in the issuer JWT's `_sd` array) — it does NOT check who signed
// the issuer JWT. That's IIssuerKeyResolver + SdJwtWalletVerifier's job, kept
// separate so a caller can never confuse "parses cleanly" with "is trustworthy".
//
// Known limitations (documented rather than silently unsupported — see ADR-0013):
//   - only top-level `_sd` digests are resolved; recursive/nested disclosure
//     (a disclosed claim that itself contains further `_sd` digests) is not handled.
//   - `_sd_alg` is assumed to be sha-256, the only value Attachment A allows for
//     in-scope credentials; a different `_sd_alg` is treated as a parse failure.
public static class SdJwtVc
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    public static SdJwtVcParseResult Parse(string sdJwtVc)
    {
        var raw = sdJwtVc.Trim();
        var parts = raw.Split('~');
        if (parts.Length < 1 || string.IsNullOrEmpty(parts[0]))
            throw new FormatException("SD-JWT VC is missing the issuer-signed JWT");

        var issuerJwt = parts[0];
        var issuerToken = Handler.ReadJwtToken(issuerJwt);

        string? keyBindingJwt = null;
        IEnumerable<string> disclosureParts;
        if (raw.EndsWith('~'))
        {
            disclosureParts = parts.Skip(1).Where(p => p.Length > 0);
        }
        else if (parts.Length > 1 && parts[^1].Count(c => c == '.') == 2)
        {
            keyBindingJwt = parts[^1];
            disclosureParts = parts.Skip(1).Take(parts.Length - 2);
        }
        else
        {
            disclosureParts = parts.Skip(1);
        }

        if (issuerToken.Payload.TryGetValue("_sd_alg", out var algVal) &&
            algVal?.ToString() is { } alg && !alg.Equals("sha-256", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"_sd_alg '{alg}' is not supported — only sha-256");

        var expectedDigests = new HashSet<string>(
            issuerToken.Payload.TryGetValue("_sd", out var sdVal) ? ExtractStringArray(sdVal) : []);

        var disclosed = new Dictionary<string, string>();
        var tampered = new List<string>();

        foreach (var d in disclosureParts)
        {
            var digest = ComputeDigest(d);
            if (!expectedDigests.Remove(digest))
            {
                tampered.Add(d);
                continue;
            }

            using var doc = JsonDocument.Parse(Base64UrlDecode(d));
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() != 3)
            {
                tampered.Add(d); // not an object-property disclosure — array-element form unsupported
                continue;
            }

            var name = arr[1].GetString() ?? throw new FormatException("disclosure claim name was not a string");
            var value = arr[2].ValueKind == JsonValueKind.String ? arr[2].GetString()! : arr[2].GetRawText();
            disclosed[name] = value;
        }

        return new SdJwtVcParseResult(
            IssuerJwt: issuerJwt,
            IssuerToken: issuerToken,
            DisclosedClaims: disclosed,
            UndisclosedDigestCount: expectedDigests.Count,
            TamperedDisclosures: tampered,
            KeyBindingJwt: keyBindingJwt);
    }

    // A JWT payload read back via JwtSecurityTokenHandler().ReadJwtToken() exposes
    // array-valued claims as a System.Text.Json.JsonElement (ValueKind.Array), not
    // List<object> — only scalar claims come back as plain CLR types. `_sd` is
    // always an array, so it always needs this path.
    private static IEnumerable<string> ExtractStringArray(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.Array } el =>
            el.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).OfType<string>(),
        IEnumerable<object> list => list.Select(o => o?.ToString()).OfType<string>(),
        _ => [],
    };

    private static string ComputeDigest(string disclosure)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(disclosure));
        return Base64UrlEncode(hash);
    }

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public record SdJwtVcParseResult(
    string IssuerJwt,
    JwtSecurityToken IssuerToken,
    IReadOnlyDictionary<string, string> DisclosedClaims,
    int UndisclosedDigestCount,
    IReadOnlyList<string> TamperedDisclosures,
    string? KeyBindingJwt
);
