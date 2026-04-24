namespace ImageRelay.Api.Services;

internal enum ProxyRequestBodyErrorType
{
    None = 0,
    InvalidJson = 1,
    MissingModel = 2
}

internal readonly record struct ProxyRequestBodyParseResult(
    ProxyRequestBodyErrorType ErrorType,
    string? Model,
    string? ErrorMessage)
{
    public bool IsSuccess => ErrorType == ProxyRequestBodyErrorType.None;

    public static ProxyRequestBodyParseResult Success(string model) =>
        new(ProxyRequestBodyErrorType.None, model, null);

    public static ProxyRequestBodyParseResult InvalidJson(string? errorMessage) =>
        new(ProxyRequestBodyErrorType.InvalidJson, null, errorMessage);

    public static ProxyRequestBodyParseResult MissingModel() =>
        new(ProxyRequestBodyErrorType.MissingModel, null, null);
}

internal static class ProxyRequestBodyHelper
{
    internal static ProxyRequestBodyParseResult ParseAndExtractModel(string rawBody)
    {
        using var doc = TryParse(rawBody, out var parseError);
        if (doc is null)
        {
            return ProxyRequestBodyParseResult.InvalidJson(parseError);
        }

        if (!doc.RootElement.TryGetProperty("model", out var modelElement) || modelElement.ValueKind != JsonValueKind.String)
        {
            return ProxyRequestBodyParseResult.MissingModel();
        }

        var model = modelElement.GetString();
        return string.IsNullOrWhiteSpace(model)
            ? ProxyRequestBodyParseResult.MissingModel()
            : ProxyRequestBodyParseResult.Success(model);
    }

    internal static string RewriteModel(string body, string upstreamModel)
    {
        using var doc = JsonDocument.Parse(body);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("model"))
                {
                    writer.WriteString("model", upstreamModel);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static JsonDocument? TryParse(string raw, out string? error)
    {
        try
        {
            error = null;
            return JsonDocument.Parse(raw);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
