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
}
