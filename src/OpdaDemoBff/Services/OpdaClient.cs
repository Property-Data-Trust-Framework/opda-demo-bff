using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Logging;
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
    private readonly bool _disconnected;
    private readonly ILogger<OpdaClient> _log;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public static async Task<OpdaClient> CreateAsync(OpdaClientConfig cfg, ILogger<OpdaClient> log)
    {
        using var ssm = new AmazonSimpleSystemsManagementClient();

        var certPem = await GetParam(ssm, cfg.ClientCertPath, withDecryption: false);
        var keyPem  = await GetParam(ssm, cfg.ClientKeyPath,  withDecryption: true);
        var sigPem  = await GetParam(ssm, cfg.SigningKeyPath, withDecryption: true);

        var mtlsCert = X509Certificate2.CreateFromPem(certPem, keyPem);

        // Both the token endpoint and the OPDA API need the same mTLS cert.
        // Token handler bypasses server-cert validation — see BuildMtlsHandler.
        var apiHandler   = BuildMtlsHandler(mtlsCert);
        var tokenHandler = BuildMtlsHandler(mtlsCert, trustOpdaSandboxCa: true);

        var rsa = RSA.Create();
        rsa.ImportFromPem(sigPem);

        var apiClient = new HttpClient(apiHandler) { BaseAddress = new Uri(cfg.ApiBaseUrl) };
        if (!string.IsNullOrEmpty(cfg.ApiKeyPath))
        {
            var apiKey = await GetParam(ssm, cfg.ApiKeyPath, withDecryption: true);
            apiClient.DefaultRequestHeaders.Add(cfg.ApiKeyHeaderName, apiKey);
        }

        return new OpdaClient(
            apiClient:     apiClient,
            tokenClient:   new HttpClient(tokenHandler),
            signingKey:    rsa,
            clientId:      cfg.ClientId,
            tokenEndpoint: cfg.TokenEndpoint,
            scope:         cfg.Scope,
            disconnected:  cfg.Disconnected,
            log:           log);
    }

    private OpdaClient(HttpClient apiClient, HttpClient tokenClient, RSA signingKey,
                       string clientId, string tokenEndpoint, string scope, bool disconnected,
                       ILogger<OpdaClient> log)
    {
        _apiClient     = apiClient;
        _tokenClient   = tokenClient;
        _signingKey    = signingKey;
        _clientId      = clientId;
        _tokenEndpoint = tokenEndpoint;
        _scope         = scope;
        _disconnected  = disconnected;
        _log           = log;
    }

    // Socket-level failures (DNS, connection refused/reset, timeout) used to
    // propagate as exceptions → 500 from the endpoint. Downstream unreachability
    // is an expected state (partner APIs, disconnected sandbox), so it maps to
    // null exactly like a clean upstream 5xx does.
    private async Task<HttpResponseMessage?> SendSafeAsync(HttpRequestMessage req, CancellationToken ct)
    {
        try
        {
            return await _apiClient.SendAsync(req, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            _log.LogError("Upstream unreachable for {Scope} {Method} {Path}: {Error}",
                _scope, req.Method, req.RequestUri, e.Message);
            return null;
        }
    }

    public async Task<JsonElement?> GetAsync(string path, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) { _log.LogError("Token acquisition failed for {Scope} GET {Path}", _scope, path); return null; }
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new("Bearer", token);
        var res = await SendSafeAsync(req, ct);
        if (res is null) return null;
        if (!res.IsSuccessStatusCode)
        {
            var errBody = await res.Content.ReadAsStringAsync(ct);
            _log.LogError("Upstream {Status} for {Scope} GET {Path}: {Body}", (int)res.StatusCode, _scope, path, errBody);
            return null;
        }
        return await res.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    public async Task<JsonElement?> PostAsync(string path, object body, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) { _log.LogError("Token acquisition failed for {Scope} POST {Path}", _scope, path); return null; }
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new("Bearer", token);
        req.Content = JsonContent.Create(body);
        var reqBody = System.Text.Json.JsonSerializer.Serialize(body);
        _log.LogInformation("Outbound {Scope} POST {Path}: {Body}", _scope, path, reqBody);
        var res = await SendSafeAsync(req, ct);
        if (res is null) return null;
        if (!res.IsSuccessStatusCode)
        {
            var errBody = await res.Content.ReadAsStringAsync(ct);
            _log.LogError("Upstream {Status} for {Scope} POST {Path}: {Body}", (int)res.StatusCode, _scope, path, errBody);
            return null;
        }
        return await res.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    public async Task<(JsonElement? Body, string? JwsSignature)> GetWithJwsAsync(string path, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) return (null, null);
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new("Bearer", token);
        var res = await SendSafeAsync(req, ct);
        if (res is null) return (null, null);
        if (!res.IsSuccessStatusCode)
        {
            var errBody = await res.Content.ReadAsStringAsync(ct);
            _log.LogError("Upstream {Status} for {Scope} GET (JWS) {Path}: {Body}", (int)res.StatusCode, _scope, path, errBody);
            return (null, null);
        }
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
        var jws  = res.Headers.TryGetValues("x-jws-signature", out var vals) ? vals.FirstOrDefault() : null;
        return (body, jws);
    }

    public async Task<(JsonElement? Body, string? JwsSignature)> PostWithJwsAsync(string path, object body, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) return (null, null);
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new("Bearer", token);
        req.Content = JsonContent.Create(body);
        var res = await SendSafeAsync(req, ct);
        if (res is null) return (null, null);
        if (!res.IsSuccessStatusCode)
        {
            var errBody = await res.Content.ReadAsStringAsync(ct);
            _log.LogError("Upstream {Status} for {Scope} POST (JWS) {Path}: {Body}", (int)res.StatusCode, _scope, path, errBody);
            return (null, null);
        }
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
        var jws  = res.Headers.TryGetValues("x-jws-signature", out var vals) ? vals.FirstOrDefault() : null;
        return (json, jws);
    }

    public async Task<JsonElement?> PostFormAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) return null;
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new("Bearer", token);
        req.Content = new FormUrlEncodedContent(fields);
        var res = await SendSafeAsync(req, ct);
        if (res is null) return null;
        return res.IsSuccessStatusCode
            ? await res.Content.ReadFromJsonAsync<JsonElement>(ct)
            : null;
    }

    // ── token acquisition ─────────────────────────────────────────────────────

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        // Disconnected sandbox: no token infrastructure exists. The shared proxy only
        // requires a non-empty Bearer header and the authorizers run bypassed.
        if (_disconnected) return "sandbox-disconnected";

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

            HttpResponseMessage res;
            try
            {
                res = await _tokenClient.PostAsync(_tokenEndpoint, form, ct);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                _log.LogError("Token endpoint unreachable for scope {Scope}: {Error}", _scope, e.Message);
                return null;
            }
            if (!res.IsSuccessStatusCode)
            {
                var detail = await res.Content.ReadAsStringAsync(ct);
                _log.LogError("Token endpoint {Status} for scope {Scope}: {Body}", (int)res.StatusCode, _scope, detail);
                return null;
            }

            var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
            _cachedToken = json.GetProperty("access_token").GetString();
            if (_cachedToken is null) return null;
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

    private static HttpClientHandler BuildMtlsHandler(X509Certificate2 cert, bool trustOpdaSandboxCa = false)
    {
        var h = new HttpClientHandler();
        h.ClientCertificates.Add(cert);
        // The OPDA Sandbox Issuing CA is not in the standard system trust store.
        // Auth is covered by mTLS + private_key_jwt, so server-cert validation on
        // the token endpoint is bypassed rather than bundling the private CA cert.
        if (trustOpdaSandboxCa)
            h.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
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
