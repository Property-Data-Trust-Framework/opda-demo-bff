using System.Text.Json;
using System.Text.Json.Nodes;
using OpdaDemoBff.Services;
using Xunit;

namespace OpdaDemoBff.Tests;

public class PdtfPackAssemblerTests
{
    private static JsonElement El(string json) => JsonDocument.Parse(json).RootElement;

    // Signed shape: { data: { propertyPack: {...} }, provenance: {...} }
    private static readonly JsonElement Epc = El(
        """{"data":{"propertyPack":{"energyEfficiency":{"certificate":{"currentEnergyRating":"C"}}}},"provenance":{"alg":"RS256"}}""");
    private static readonly JsonElement CouncilTax = El(
        """{"data":{"propertyPack":{"councilTax":{"councilTaxBand":"D"}}},"provenance":{"alg":"RS256"}}""");
    private static readonly JsonElement Mra = El(
        """{"data":{"propertyPack":{"environmentalIssues":{"coalMining":{"riskIndicator":"Yes"}}}},"provenance":{"alg":"RS256"}}""");
    private static readonly JsonElement Facade = El(
        """{"data":{"propertyPack":{"titlesToBeSold":[{"registerExtract":{"ocSummaryData":{"title":{"titleNumber":"EXC10010"}}}}]}},"provenance":{"alg":"RS256"}}""");

    [Fact]
    public void Assemble_MergesAllFragmentsIntoOnePack()
    {
        var result = PdtfPackAssembler.Assemble(
            ("epc", Epc), ("councilTax", CouncilTax), ("coalfield", Mra), ("titleRegister", Facade));
        var pack = result["propertyPack"]!;

        Assert.Equal("C", pack["energyEfficiency"]!["certificate"]!["currentEnergyRating"]!.GetValue<string>());
        Assert.Equal("D", pack["councilTax"]!["councilTaxBand"]!.GetValue<string>());
        Assert.Equal("Yes", pack["environmentalIssues"]!["coalMining"]!["riskIndicator"]!.GetValue<string>());
        Assert.Equal("EXC10010", pack["titlesToBeSold"]![0]!["registerExtract"]!["ocSummaryData"]!["title"]!["titleNumber"]!.GetValue<string>());
    }

    [Fact]
    public void Assemble_SurfacesPerSourceProvenance()
    {
        var result = PdtfPackAssembler.Assemble(
            ("epc", Epc), ("councilTax", CouncilTax), ("coalfield", Mra), ("titleRegister", Facade));
        var provenance = (JsonObject)result["provenance"]!;

        Assert.Equal(4, provenance.Count);
        Assert.Equal("RS256", provenance["epc"]!["alg"]!.GetValue<string>());
        Assert.Equal("RS256", provenance["titleRegister"]!["alg"]!.GetValue<string>());
    }

    [Fact]
    public void Assemble_SkipsNullAndFragmentlessResponses()
    {
        var empty = El("""{"data":{"uprn":"100023336956","councilTaxBand":"D"}}"""); // flat, no propertyPack
        var result = PdtfPackAssembler.Assemble(("epc", Epc), ("councilTax", null), ("coalfield", empty));
        var pack = (JsonObject)result["propertyPack"]!;
        var provenance = (JsonObject)result["provenance"]!;

        Assert.True(pack.ContainsKey("energyEfficiency"));
        Assert.False(pack.ContainsKey("councilTax"));   // the flat response contributes nothing
        Assert.True(provenance.ContainsKey("epc"));
        Assert.False(provenance.ContainsKey("councilTax")); // null response -> no provenance entry
    }

    [Fact]
    public void Assemble_OmitsProvenanceEntryForUnsignedSource()
    {
        var unsigned = El("""{"propertyPack":{"councilTax":{"councilTaxBand":"E"}}}""");
        var result = PdtfPackAssembler.Assemble(("councilTax", unsigned));
        var pack = (JsonObject)result["propertyPack"]!;
        var provenance = (JsonObject)result["provenance"]!;

        Assert.Equal("E", pack["councilTax"]!["councilTaxBand"]!.GetValue<string>());
        Assert.Empty(provenance);
    }

    [Fact]
    public void ExtractPropertyPack_HandlesUnsignedShape()
    {
        var unsigned = El("""{"propertyPack":{"councilTax":{"councilTaxBand":"E"}}}""");
        var pack = PdtfPackAssembler.ExtractPropertyPack(unsigned);
        Assert.Equal("E", pack!["councilTax"]!["councilTaxBand"]!.GetValue<string>());
    }

    [Fact]
    public void ExtractPropertyPack_ReturnsNullForNull() =>
        Assert.Null(PdtfPackAssembler.ExtractPropertyPack(null));
}
