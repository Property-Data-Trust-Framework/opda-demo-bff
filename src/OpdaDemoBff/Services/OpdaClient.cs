using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.IdentityModel.Tokens;
using OpdaDemoBff.Config;

namespace OpdaDemoBff.Services;

public sealed class OpdaClient : IOpdaClient, IDisposable
{
    private readonly HttpClient _apiClient;
    private readonly HttpClient _tokenClient;
    private readonly RSA _signingKey;
    private readonly string _clientId;
    private readonly string _tokenEndpoint;
    private readonly string _scope;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public static async Task<OpdaClient> CreateAsync(OpdaClientConfig cfg)
    {
        using var ssm = new AmazonSimpleSystemsManagementClient();

        var certPem = await GetParam(ssm, cfg.ClientCertPath, withDecryption: false);
        var keyPem  = await GetParam(ssm, cfg.ClientKeyPath,  withDecryption: true);
        var sigPem  = await GetParam(ssm, cfg.SigningKeyPath, withDecryption: true);

        var mtlsCert = X509Certificate2.CreateFromPem(certPem, keyPem);

        // Both the token endpoint and the OPDA API need the same mTLS cert
        var apiHandler   = BuildMtlsHandler(mtlsCert);
        var tokenHandler = BuildMtlsHandler(mtlsCert);

        var rsa = RSA.Create();
        rsa.ImportFromPem(sigPem);

        return new OpdaClient(
            apiClient:     new HttpClient(apiHandler)   { BaseAddress = new Uri(cfg.ApiBaseUrl) },
            tokenClient:   new HttpClient(tokenHandler),
            signingKey:    rsa,
            clientId:      cfg.ClientId,
            tokenEndpoint: cfg.TokenEndpoint,
            scope:         cfg.Scope);
    }

    private OpdaClient(HttpClient apiClient, HttpClient tokenClient, RSA signingKey,
                       string clientId, string tokenEndpoint, string scope)
    {
        _apiClient     = apiClient;
        _tokenClient   = tokenClient;
        _signingKey    = signingKey;
        _clientId      = clientId;
        _tokenEndpoint = tokenEndpoint;
        _scope         = scope;
    }

    public async Task<JsonElement?> GetAsync(string path, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new("Bearer", token);
        var res = await _apiClient.SendAsync(req, ct);
        return res.IsSuccessStatusCode
            ? await res.Content.ReadFromJsonAsync<JsonElement>(ct)
            : null;
    }

    public async Task<JsonElement?> PostAsync(string path, object body, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new("Bearer", token);
        req.Content = JsonContent.Create(body);
        var res = await _apiClient.SendAsync(req, ct);
        return res.IsSuccessStatusCode
            ? await res.Content.ReadFromJsonAsync<JsonElement>(ct)
            : null;
    }

    public async Task<JsonElement?> PostFormAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new("Bearer", token);
        req.Content = new FormUrlEncodedContent(fields);
        var res = await _apiClient.SendAsync(req, ct);
        return res.IsSuccessStatusCode
            ? await res.Content.ReadFromJsonAsync<JsonElement>(ct)
            : null;
    }

    // ── token acquisition ─────────────────────────────────────────────────────

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiry.AddMinutes(-1))
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiry.AddMinutes(-1))
                return _cachedToken;

            var assertion = BuildClientAssertion();
            var form = new FormUrlEncodedContent([
                new("grant_type",            "client_credentials"),
                new("client_id",             _clientId),
                new("client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
                new("client_assertion",      assertion),
                new("scope",                 _scope),
            ]);

            var res = await _tokenClient.PostAsync(_tokenEndpoint, form, ct);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
            _cachedToken = json.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Token response missing access_token");
            var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 300;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            return _cachedToken;
        }
        finally { _tokenLock.Release(); }
    }

    private string BuildClientAssertion()
    {
        var key   = new RsaSecurityKey(_signingKey);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var now   = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer:             _clientId,
            audience:           _tokenEndpoint,
            claims:             [
                new Claim(JwtRegisteredClaimNames.Sub, _clientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore:          now,
            expires:            now.AddMinutes(5),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<string> GetParam(IAmazonSimpleSystemsManagement ssm, string name, bool withDecryption)
    {
        var res = await ssm.GetParameterAsync(new GetParameterRequest
            { Name = name, WithDecryption = withDecryption });
        return res.Parameter.Value;
    }

    private static HttpClientHandler BuildMtlsHandler(X509Certificate2 cert)
    {
        var h = new HttpClientHandler();
        h.ClientCertificates.Add(cert);
        return h;
    }

    public void Dispose()
    {
        _apiClient.Dispose();
        _tokenClient.Dispose();
        _signingKey.Dispose();
        _tokenLock.Dispose();
    }
}
