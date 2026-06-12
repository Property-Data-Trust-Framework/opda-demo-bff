namespace OpdaDemoBff.Config;

public class OpdaClientConfig
{
    public string ApiBaseUrl     { get; init; } = "";
    public string ClientCertPath { get; init; } = "";
    public string ClientKeyPath  { get; init; } = "";
    public string SigningKeyPath  { get; init; } = "";
    public string ClientId        { get; init; } = "";
    public string TokenEndpoint   { get; init; } = "";
    public string Scope           { get; init; } = "land-registry";
    // Optional extra header loaded from SSM — used by Sprift sandbox (x-api-key).
    public string? ApiKeyPath       { get; init; }
    public string  ApiKeyHeaderName { get; init; } = "x-api-key";
}
