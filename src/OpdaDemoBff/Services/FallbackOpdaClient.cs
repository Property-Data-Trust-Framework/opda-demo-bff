using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpdaDemoBff.Services;

/// <summary>
/// DISCONNECTED_MODE decorator (ADR-0012): when the wrapped client returns null
/// (upstream unreachable, auth gone, partner de-registered), serves a canned
/// fixture for known routes so the demo stays walkable with zero external
/// dependencies. Fixtures mirror the LIVE response shapes — PDTF v3.5
/// propertyPack fragments inside signed {data, provenance} envelopes for the
/// pack APIs, the real envelope shapes elsewhere (same shapes as the SPA's
/// offline PAYLOADS, which live data replaces wholesale).
///
/// Only registered when DISCONNECTED_MODE=true — in connected mode a dead
/// upstream stays a visible 502, not silently-canned data. Every served
/// fixture logs a warning, so fallbacks are diagnosable in CloudWatch.
/// </summary>
public sealed class FallbackOpdaClient(IOpdaClient inner, ILogger<FallbackOpdaClient> log) : IOpdaClient
{
    public async Task<JsonElement?> GetAsync(string path, CancellationToken ct = default)
        => await inner.GetAsync(path, ct) ?? Fixture(path);

    public async Task<JsonElement?> PostAsync(string path, object body, CancellationToken ct = default)
        => await inner.PostAsync(path, body, ct) ?? Fixture(path);

    public async Task<JsonElement?> PostFormAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct = default)
        => await inner.PostFormAsync(path, fields, ct) ?? Fixture(path);

    public async Task<(JsonElement? Body, string? JwsSignature)> GetWithJwsAsync(string path, CancellationToken ct = default)
    {
        var (body, jws) = await inner.GetWithJwsAsync(path, ct);
        return body is not null ? (body, jws) : (Fixture(path), null);
    }

    public async Task<(JsonElement? Body, string? JwsSignature)> PostWithJwsAsync(string path, object body, CancellationToken ct = default)
    {
        var (respBody, jws) = await inner.PostWithJwsAsync(path, body, ct);
        return respBody is not null ? (respBody, jws) : (Fixture(path), null);
    }

    private JsonElement? Fixture(string path)
    {
        foreach (var (prefix, json) in Fixtures)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                log.LogWarning("DISCONNECTED_MODE fallback served for {Path} (matched {Prefix})", path, prefix);
                return JsonSerializer.Deserialize<JsonElement>(json);
            }
        }
        log.LogWarning("DISCONNECTED_MODE: no fallback fixture for {Path} — returning null", path);
        return null;
    }

    // A provenance block shaped like the real signer output. The signature is a
    // fixture, not a verifiable one — honest about that via the kid.
    private const string Prov = """
        {"alg":"RS256","kid":"disconnected-fixture","signature":"RklYVFVSRV9OT1RfQV9SRUFMX1NJR05BVFVSRQ",
         "signedAt":"2026-06-11T09:14:22Z","payloadHash":{"alg":"SHA-256","value":"RklYVFVSRV9IQVNI"}}
        """;

    private static string WithProv(string json) => json.Replace("__PROV__", Prov);

    private static readonly (string Prefix, string Json)[] RawFixtures =
    [
        ("/v1/places/find", """
            {"data":[{"uprn":"100091234567","address":"14, ELM GROVE, REDLAND, BRISTOL, BS6 5DB",
              "udprn":"21929808","xCoordinate":358205,"yCoordinate":174894,
              "localAuthority":"BRISTOL CITY COUNCIL","propertyType":"Terraced"}],
             "provenance":__PROV__}
            """),
        ("/v1/uprn/validate/", """
            {"data":{"valid":true},"provenance":__PROV__}
            """),
        ("/v1/epc/", """
            {"data":{"propertyPack":{"energyEfficiency":{"certificate":{
              "certificateNumber":"8206-7942-1030-8846-9002",
              "address":"14 Elm Grove, Redland, Bristol, BS6 5DB","address1":"14 Elm Grove",
              "postcode":"BS6 5DB","posttown":"Bristol",
              "localAuthorityLabel":"Bristol City Council","constituencyLabel":"Bristol West",
              "currentEnergyRating":"C","potentialEnergyRating":"B","lodgementDate":"2021-07-20"}}}},
             "provenance":__PROV__}
            """),
        ("/v1/council-tax/", """
            {"data":{"propertyPack":{"councilTax":{"councilTaxBand":"D"}}},"provenance":__PROV__}
            """),
        ("/v1/coalfield/", """
            {"data":{"propertyPack":{"environmentalIssues":{"coalMining":{"riskIndicator":"No"}}}},
             "provenance":__PROV__}
            """),
        ("/opda/official-copies/v1/register-extract", """
            {"data":{"propertyPack":{"titlesToBeSold":[{"registerExtract":{"ocSummaryData":{
              "title":{"titleNumber":"EXC10010","classOfTitleCode":"A"},
              "registerEntryIndicators":{"leaseHoldTitleIndicator":false},
              "propertyAddress":{"postcodeZone":{"postcode":"BS6 5DB"}},
              "proprietorship":{"registeredProprietorParty":[{"name":{"forenamesName":"A N","surname":"Seller"}}]},
              "pricePaidEntry":{"infills":{"amount":"£150,000"}}}}}]}},
             "provenance":__PROV__}
            """),
        ("/v1/documents/", """
            {"data":{"uprn":"100091234567","documents":[
              {"documentType":"full_buyer_report","filename":"full_buyer_report-01.pdf",
               "url":"about:blank#disconnected-fixture","expiresAt":"2099-01-01T00:00:00Z"},
              {"documentType":"full_homeowner_report","filename":"full_homeowner_report.pdf",
               "url":"about:blank#disconnected-fixture","expiresAt":"2099-01-01T00:00:00Z"}]},
             "provenance":__PROV__}
            """),
        ("/v1/source-of-funds/", """
            {"data":{"reportId":"rep_8f2c41","reportType":"SOURCE_OF_FUNDS",
              "issuedAt":"2026-06-11T10:02:15Z","status":"AVAILABLE","applicantName":"Robert Malytix",
              "proofOfFunds":{"totalBalance":68450.12,"formattedTotalBalance":"£68,450.12","currency":"GBP",
                "amountRequired":62000,"formattedAmountRequired":"£62,000.00",
                "surplus":6450.12,"formattedSurplus":"£6,450.12","result":"PASS"},
              "accounts":[{"bankName":"Monzo","sortCode":"04-00-04","accountNumber":"••••1234","accountName":"R Malytix"}],
              "income":{"averageMonthlyTakeHome":3120.55,"formattedAverageMonthlyTakeHome":"£3,120.55",
                "sources":[{"type":"SALARY","description":"ACME LTD","averageMonthly":3120.55,
                  "formattedAverageMonthly":"£3,120.55","verified":true}]},
              "flags":[]},
             "provenance":__PROV__}
            """),
        ("/api/v1/opda/chains", """
            {"data":[{"properties":[
              {"uprn":"200001858100","address":"2 Hill View, Bristol","position":1},
              {"uprn":"100091234567","address":"14 Elm Grove, Redland, Bristol BS6 5DB","position":2},
              {"uprn":"200001858900","address":"8 Orchard Way, Bristol","position":3}],
             "milestones":[{"label":"Offer Accepted","date":"2026-05-12"},{"label":"SSTC","date":"2026-05-20"}]}]}
            """),
        ("/opda-opaque/appraisal/v1/property-pack/uprn", """
            {"propertyPack":{"address":{"line1":"14 Elm Grove","town":"Bristol","postcode":"BS6 5DB"},
             "priceInformation":{"price":475000,"priceQualifier":"Guide price"},
             "councilTax":{"councilTaxBand":"D"}}}
            """),
        ("/opda-opaque/current-state/", """
            {"status":"For Sale","address":{"line1":"14 Elm Grove","town":"Bristol","postcode":"BS6 5DB"},
             "lastUpdated":"2026-06-11T09:00:00Z"}
            """),
        ("/opda-opaque/claims/", """
            [{"verification":{"trust_framework":"uk_pdtf","evidence":[]},
              "claims":{"councilTax":{"councilTaxBand":"D"}}}]
            """),
        ("/metainformation/", """
            {"uprn":"100091234567","partA":{"tenure":"Freehold","councilTaxBand":"D","price":475000},
             "partB":{"propertyType":"Terraced","construction":"Standard"},
             "partC":{"floodRisk":"Very low","conservationArea":false}}
            """),
        ("/property/", """
            {"uprn":"100091234567","epc":{"currentEnergyRating":"C"},"councilTax":{"band":"D"},
             "floodRisk":"Very low","avm":{"estimate":475000}}
            """),
    ];

    private static readonly (string Prefix, string Json)[] Fixtures =
        [.. RawFixtures.Select(f => (f.Prefix, WithProv(f.Json)))];
}
