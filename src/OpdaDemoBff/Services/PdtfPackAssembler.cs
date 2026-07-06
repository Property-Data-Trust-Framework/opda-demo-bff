using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpdaDemoBff.Services;

/// <summary>
/// Assembles a single PDTF v3.5 <c>propertyPack</c> from the individual OPDA
/// APIs' responses. Each API returns a provenance-signed
/// <c>{ data: { propertyPack: {...} }, provenance }</c> fragment covering a
/// disjoint slice of the pack (energyEfficiency, councilTax,
/// environmentalIssues.coalMining, titlesToBeSold). This deep-merges those
/// fragments into one pack and surfaces each source's provenance block keyed
/// by source name.
///
/// Note: the merged pack is NOT re-signed — the BFF is an aggregator, not a
/// signer. Each source fragment remains individually verifiable upstream.
/// </summary>
public static class PdtfPackAssembler
{
    /// <summary>
    /// Still sent to the downstream APIs so the BFF works against deployments
    /// from before the PDTF v3.5 shape became their default (a no-op after).
    /// Remove once all four APIs are redeployed.
    /// </summary>
    public const string SchemaSelector = "pdtf-v3.5";

    /// <summary>
    /// Merge the propertyPack fragments from each signed response into a single
    /// <c>{ "propertyPack": {...}, "provenance": { sourceKey: {...} } }</c>.
    /// Null / fragment-less responses are skipped; provenance entries appear
    /// only for sources that supplied one.
    /// </summary>
    public static JsonObject Assemble(params (string Key, JsonElement? Response)[] sources)
    {
        var merged = new JsonObject();
        var provenance = new JsonObject();
        foreach (var (key, resp) in sources)
        {
            if (ExtractPropertyPack(resp) is JsonObject fragment)
                DeepMerge(merged, fragment);
            if (ExtractProvenance(resp) is JsonObject prov)
                provenance[key] = prov;
        }
        return new JsonObject { ["propertyPack"] = merged, ["provenance"] = provenance };
    }

    /// <summary>
    /// From a signed <c>{ data: { propertyPack }, provenance }</c> response (or an
    /// unsigned <c>{ propertyPack }</c>), return a detached copy of the propertyPack
    /// object, or null if absent.
    /// </summary>
    public static JsonObject? ExtractPropertyPack(JsonElement? response)
    {
        if (response is not JsonElement el || el.ValueKind != JsonValueKind.Object)
            return null;

        var root = el;
        if (el.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            root = data;

        if (!root.TryGetProperty("propertyPack", out var pp) || pp.ValueKind != JsonValueKind.Object)
            return null;

        return JsonNode.Parse(pp.GetRawText()) as JsonObject;
    }

    /// <summary>
    /// From a signed <c>{ data, provenance }</c> response, return a detached copy
    /// of the provenance block, or null if absent (e.g. unsigned responses).
    /// </summary>
    public static JsonObject? ExtractProvenance(JsonElement? response)
    {
        if (response is not JsonElement el || el.ValueKind != JsonValueKind.Object)
            return null;

        if (!el.TryGetProperty("provenance", out var prov) || prov.ValueKind != JsonValueKind.Object)
            return null;

        return JsonNode.Parse(prov.GetRawText()) as JsonObject;
    }

    /// <summary>
    /// Recursively merge <paramref name="source"/> into <paramref name="target"/>:
    /// object+object recurses; anything else overwrites. In practice the fragments
    /// occupy disjoint paths, so overwrites don't collide.
    /// </summary>
    private static void DeepMerge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (target[key] is JsonObject targetObj && value is JsonObject sourceObj)
                DeepMerge(targetObj, sourceObj);
            else
                target[key] = value?.DeepClone();
        }
    }
}
