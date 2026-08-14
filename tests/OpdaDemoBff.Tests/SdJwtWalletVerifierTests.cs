using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using OpdaDemoBff.Services;
using Xunit;

namespace OpdaDemoBff.Tests;

// Builds real, correctly-signed SD-JWT VCs for tests rather than hardcoding
// magic strings — doubles as a worked example of the wire format for whoever
// extends this next (see verifiable_credentials_scope.md, Attachment A).
public class SdJwtWalletVerifierTests
{
    private const string Issuer = "https://credentials.example/mortgage-offer";

    private static (string SdJwt, RSA Rsa) BuildCredential(
        string vct, IDictionary<string, string> disclosedClaims, string? kid = null, string? nonce = null)
    {
        var rsa = RSA.Create(2048);
        var disclosures = new List<string>();
        var digests = new List<string>();

        foreach (var (name, value) in disclosedClaims)
        {
            var salt = Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
            var arrJson = JsonSerializer.Serialize(new object[] { salt, name, value });
            var disclosure = Base64UrlEncode(Encoding.UTF8.GetBytes(arrJson));
            disclosures.Add(disclosure);
            digests.Add(Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(disclosure))));
        }

        // Built via the JwtPayload dictionary API rather than a List<Claim> —
        // `_sd` needs to serialise as a genuine JSON array, and multiple
        // same-named System.Security.Claims.Claim entries don't reliably
        // aggregate into one through JwtSecurityToken's claims constructor.
        var payload = new JwtPayload
        {
            ["iss"] = Issuer,
            ["vct"] = vct,
            ["_sd_alg"] = "sha-256",
            ["_sd"] = digests,
        };
        var key = new RsaSecurityKey(rsa) { KeyId = kid };
        var header = new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        var token = new JwtSecurityToken(header, payload);
        var issuerJwt = new JwtSecurityTokenHandler().WriteToken(token);

        var kbJwt = "";
        if (nonce is not null)
        {
            var kbCreds = new SigningCredentials(new RsaSecurityKey(RSA.Create(2048)), SecurityAlgorithms.RsaSha256);
            var kbToken = new JwtSecurityToken(claims: [new("nonce", nonce)], signingCredentials: kbCreds);
            kbJwt = new JwtSecurityTokenHandler().WriteToken(kbToken);
        }

        var sdJwt = issuerJwt + "~" + string.Join("~", disclosures) + "~" + kbJwt;
        return (sdJwt, rsa);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ── SdJwtVc.Parse ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCredential_ExtractsDisclosedClaims()
    {
        var (sdJwt, _) = BuildCredential("mortgage-offer", new Dictionary<string, string>
        {
            ["borrower_given_name"] = "Robert",
            ["loan_amount"] = "150000",
        });

        var result = SdJwtVc.Parse(sdJwt);

        Assert.Equal("Robert", result.DisclosedClaims["borrower_given_name"]);
        Assert.Equal("150000", result.DisclosedClaims["loan_amount"]);
        Assert.Empty(result.TamperedDisclosures);
        Assert.Equal(Issuer, result.IssuerToken.Issuer);
    }

    [Fact]
    public void Parse_DisclosureNotInSdArray_IsReportedAsTampered()
    {
        var (sdJwt, _) = BuildCredential("mortgage-offer", new Dictionary<string, string> { ["a"] = "1" });

        // Splice in a disclosure for a claim that was never added to _sd.
        var foreignDisclosure = Base64UrlEncode(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new object[] { "foreign-salt", "injected", "true" })));
        var tampered = sdJwt.TrimEnd('~') + "~" + foreignDisclosure + "~";

        var result = SdJwtVc.Parse(tampered);

        Assert.Contains(foreignDisclosure, result.TamperedDisclosures);
    }

    [Fact]
    public void Parse_UndisclosedClaims_AreCountedNotLeaked()
    {
        var (sdJwt, _) = BuildCredential("mortgage-offer", new Dictionary<string, string> { ["shown"] = "1" });
        // Drop the one disclosure so it's undisclosed rather than parsed.
        var withheld = sdJwt.Split('~')[0] + "~";

        var result = SdJwtVc.Parse(withheld);

        Assert.Empty(result.DisclosedClaims);
        Assert.Equal(1, result.UndisclosedDigestCount);
    }

    // ── SdJwtWalletVerifier ──────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_TrustedIssuer_SignatureVerifiedTrue()
    {
        var (sdJwt, rsa) = BuildCredential("mortgage-offer", new Dictionary<string, string> { ["loan_amount"] = "150000" });
        var registry = new StaticIssuerKeyResolver(JsonSerializer.Serialize(
            new Dictionary<string, string> { [Issuer] = ExportPublicKeyPem(rsa) }));
        var verifier = new SdJwtWalletVerifier(registry, NullLogger<SdJwtWalletVerifier>.Instance);

        var outcome = await verifier.VerifyAsync(sdJwt, expectedNonce: "n/a", requestedCredentialTypes: ["mortgage-offer"]);

        Assert.True(outcome.Verified);
        Assert.True(outcome.Credentials[0].SignatureVerified);
    }

    [Fact]
    public async Task VerifyAsync_UnknownIssuer_FailsClosed()
    {
        var (sdJwt, _) = BuildCredential("mortgage-offer", new Dictionary<string, string> { ["loan_amount"] = "150000" });
        var registry = new StaticIssuerKeyResolver(null); // nothing registered
        var verifier = new SdJwtWalletVerifier(registry, NullLogger<SdJwtWalletVerifier>.Instance);

        var outcome = await verifier.VerifyAsync(sdJwt, expectedNonce: "n/a", requestedCredentialTypes: ["mortgage-offer"]);

        Assert.False(outcome.Verified);
        Assert.False(outcome.Credentials[0].SignatureVerified);
        Assert.NotNull(outcome.FailureReason);
    }

    [Fact]
    public async Task VerifyAsync_WrongSigningKey_SignatureVerifiedFalse()
    {
        var (sdJwt, _) = BuildCredential("mortgage-offer", new Dictionary<string, string> { ["loan_amount"] = "150000" });
        var wrongKey = RSA.Create(2048); // registry has a *different* key for the same issuer
        var registry = new StaticIssuerKeyResolver(JsonSerializer.Serialize(
            new Dictionary<string, string> { [Issuer] = ExportPublicKeyPem(wrongKey) }));
        var verifier = new SdJwtWalletVerifier(registry, NullLogger<SdJwtWalletVerifier>.Instance);

        var outcome = await verifier.VerifyAsync(sdJwt, expectedNonce: "n/a", requestedCredentialTypes: ["mortgage-offer"]);

        Assert.False(outcome.Verified);
        Assert.False(outcome.Credentials[0].SignatureVerified);
    }

    [Fact]
    public async Task VerifyAsync_TwoCredentialsMatchingSubject_Verifies()
    {
        var offer = BuildCredential("mortgage-offer", new Dictionary<string, string>
        {
            ["borrower_given_name"] = "Robert", ["borrower_family_name"] = "Malytix", ["borrower_date_of_birth"] = "1990-01-01",
        });
        var identity = BuildCredential("person-identity", new Dictionary<string, string>
        {
            ["given_name"] = "Robert", ["family_name"] = "Malytix", ["date_of_birth"] = "1990-01-01",
        });
        var registry = new StaticIssuerKeyResolver(JsonSerializer.Serialize(
            new Dictionary<string, string> { [Issuer] = ExportPublicKeyPem(offer.Rsa) }));
        // Both credentials share the same mock issuer/key in this test fixture.
        var verifier = new SdJwtWalletVerifier(registry, NullLogger<SdJwtWalletVerifier>.Instance);

        var vpToken = JsonSerializer.Serialize(new[] { offer.SdJwt, ReplaceSignature(identity.SdJwt, offer.Rsa) });
        var outcome = await verifier.VerifyAsync(vpToken, "n/a", ["mortgage-offer", "person-identity"]);

        Assert.True(outcome.Verified);
        Assert.Equal(2, outcome.Credentials.Count);
    }

    [Fact]
    public async Task VerifyAsync_MismatchedSubjectAcrossCredentials_Fails()
    {
        var offer = BuildCredential("mortgage-offer", new Dictionary<string, string>
        {
            ["borrower_given_name"] = "Robert", ["borrower_family_name"] = "Malytix", ["borrower_date_of_birth"] = "1990-01-01",
        });
        var identity = BuildCredential("person-identity", new Dictionary<string, string>
        {
            ["given_name"] = "Someone", ["family_name"] = "Else", ["date_of_birth"] = "1980-05-05",
        });
        var registry = new StaticIssuerKeyResolver(JsonSerializer.Serialize(
            new Dictionary<string, string> { [Issuer] = ExportPublicKeyPem(offer.Rsa) }));
        var verifier = new SdJwtWalletVerifier(registry, NullLogger<SdJwtWalletVerifier>.Instance);

        var vpToken = JsonSerializer.Serialize(new[] { offer.SdJwt, ReplaceSignature(identity.SdJwt, offer.Rsa) });
        var outcome = await verifier.VerifyAsync(vpToken, "n/a", ["mortgage-offer", "person-identity"]);

        Assert.False(outcome.Verified);
        Assert.Contains("issued-to subject", outcome.FailureReason);
    }

    // Re-signs an SD-JWT's issuer JWT with a different key so two independently
    // built test credentials can share one registry entry without a second
    // registered issuer key — keeps the fixture builder single-purpose.
    private static string ReplaceSignature(string sdJwt, RSA rsa)
    {
        var parts = sdJwt.Split('~');
        var handler = new JwtSecurityTokenHandler();
        var original = handler.ReadJwtToken(parts[0]);
        var claims = original.Claims.ToList();
        var creds = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var resigned = new JwtSecurityToken(issuer: original.Issuer, claims: claims, signingCredentials: creds);
        parts[0] = handler.WriteToken(resigned);
        return string.Join("~", parts);
    }

    private static string ExportPublicKeyPem(RSA rsa) =>
        "-----BEGIN PUBLIC KEY-----\n" +
        Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks) +
        "\n-----END PUBLIC KEY-----";
}
