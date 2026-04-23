using System.Text.Json;
using ImageRelay.Api.Services;
using Xunit;

namespace ImageRelay.Api.Tests.Services;

public class ProxyRequestBodyHelperTests
{
    [Theory]
    [InlineData("image_generation_request.json")]
    [InlineData("image_parse_request.json")]
    public void ParseAndExtractModel_ReadsRootRequestFixtures(string fixtureFileName)
    {
        var body = RootRequestFixtureLoader.LoadRequestBody(fixtureFileName);

        var result = ProxyRequestBodyHelper.ParseAndExtractModel(body);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProxyRequestBodyErrorType.None, result.ErrorType);
        Assert.Equal("gpt-5.4", result.Model);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("image_generation_request.json")]
    [InlineData("image_parse_request.json")]
    public void RewriteModel_OnlyChangesTopLevelModel(string fixtureFileName)
    {
        var body = RootRequestFixtureLoader.LoadRequestBody(fixtureFileName);

        var rewrittenBody = ProxyRequestBodyHelper.RewriteModel(body, "gpt-image-2");

        using var originalDoc = JsonDocument.Parse(body);
        using var rewrittenDoc = JsonDocument.Parse(rewrittenBody);

        AssertOnlyModelChanged(originalDoc.RootElement, rewrittenDoc.RootElement, "gpt-image-2");
    }

    [Fact]
    public void ParseAndExtractModel_ReturnsInvalidJsonForMalformedBody()
    {
        var result = ProxyRequestBodyHelper.ParseAndExtractModel("{\"model\":");

        Assert.False(result.IsSuccess);
        Assert.Equal(ProxyRequestBodyErrorType.InvalidJson, result.ErrorType);
        Assert.Null(result.Model);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ParseAndExtractModel_ReturnsMissingModelWhenModelFieldIsMissing()
    {
        var result = ProxyRequestBodyHelper.ParseAndExtractModel("{\"input\":[]}");

        Assert.False(result.IsSuccess);
        Assert.Equal(ProxyRequestBodyErrorType.MissingModel, result.ErrorType);
        Assert.Null(result.Model);
        Assert.Null(result.ErrorMessage);
    }

    private static void AssertOnlyModelChanged(JsonElement original, JsonElement rewritten, string expectedModel)
    {
        Assert.Equal(JsonValueKind.Object, original.ValueKind);
        Assert.Equal(JsonValueKind.Object, rewritten.ValueKind);

        var originalProperties = original.EnumerateObject()
            .ToDictionary(prop => prop.Name, prop => prop.Value, StringComparer.Ordinal);
        var rewrittenProperties = rewritten.EnumerateObject()
            .ToDictionary(prop => prop.Name, prop => prop.Value, StringComparer.Ordinal);

        Assert.Equal(originalProperties.Count, rewrittenProperties.Count);
        Assert.True(originalProperties.ContainsKey("model"));
        Assert.True(rewrittenProperties.TryGetValue("model", out var rewrittenModel));
        Assert.Equal(expectedModel, rewrittenModel.GetString());

        foreach (var (name, originalValue) in originalProperties)
        {
            Assert.True(rewrittenProperties.TryGetValue(name, out var rewrittenValue));
            if (name == "model")
            {
                continue;
            }

            AssertJsonEquivalent(originalValue, rewrittenValue);
        }
    }

    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var expectedProperties = expected.EnumerateObject()
                    .ToDictionary(prop => prop.Name, prop => prop.Value, StringComparer.Ordinal);
                var actualProperties = actual.EnumerateObject()
                    .ToDictionary(prop => prop.Name, prop => prop.Value, StringComparer.Ordinal);

                Assert.Equal(expectedProperties.Count, actualProperties.Count);
                foreach (var (name, expectedValue) in expectedProperties)
                {
                    Assert.True(actualProperties.TryGetValue(name, out var actualValue));
                    AssertJsonEquivalent(expectedValue, actualValue);
                }
                break;
            }
            case JsonValueKind.Array:
            {
                var expectedItems = expected.EnumerateArray().ToArray();
                var actualItems = actual.EnumerateArray().ToArray();
                Assert.Equal(expectedItems.Length, actualItems.Length);
                for (var i = 0; i < expectedItems.Length; i++)
                {
                    AssertJsonEquivalent(expectedItems[i], actualItems[i]);
                }
                break;
            }
            case JsonValueKind.String:
                Assert.Equal(expected.GetString(), actual.GetString());
                break;
            case JsonValueKind.Number:
                Assert.Equal(expected.GetRawText(), actual.GetRawText());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                break;
            case JsonValueKind.Null:
                break;
            default:
                Assert.Equal(expected.GetRawText(), actual.GetRawText());
                break;
        }
    }
}
