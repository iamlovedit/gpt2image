namespace ImageRelay.Api.Services;

public sealed class ResponseTokenUsageParser
{
    private readonly StringBuilder _pending = new();

    public void Append(ReadOnlySpan<byte> chunk, RequestLog log)
    {
        if (chunk.IsEmpty) return;

        _pending.Append(Encoding.UTF8.GetString(chunk));
        ProcessCompleteEvents(log);
    }

    private void ProcessCompleteEvents(RequestLog log)
    {
        while (true)
        {
            var pending = _pending.ToString();
            var separatorIndex = FindEventSeparator(pending);
            if (separatorIndex < 0) return;

            var separatorLength = pending[separatorIndex] == '\r' ? 4 : 2;
            var rawEvent = pending[..separatorIndex];
            _pending.Remove(0, separatorIndex + separatorLength);

            TryApplyEvent(rawEvent, log);
        }
    }

    private static int FindEventSeparator(string value)
    {
        var lf = value.IndexOf("\n\n", StringComparison.Ordinal);
        var crlf = value.IndexOf("\r\n\r\n", StringComparison.Ordinal);

        if (lf < 0) return crlf;
        if (crlf < 0) return lf;
        return Math.Min(lf, crlf);
    }

    private static void TryApplyEvent(string rawEvent, RequestLog log)
    {
        var data = ReadDataPayload(rawEvent);
        if (string.IsNullOrWhiteSpace(data)) return;

        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String
                || typeElement.GetString() != "response.completed")
            {
                return;
            }

            if (!root.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object)
                return;

            ApplyUsage(response, log);
            ApplyImageUsage(response, log);
        }
        catch (JsonException)
        {
        }
    }

    private static string ReadDataPayload(string rawEvent)
    {
        var builder = new StringBuilder();
        using var reader = new StringReader(rawEvent);

        while (reader.ReadLine() is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var dataLine = line[5..];
            if (dataLine.StartsWith(' ')) dataLine = dataLine[1..];

            if (builder.Length > 0) builder.Append('\n');
            builder.Append(dataLine);
        }

        return builder.ToString();
    }

    private static void ApplyUsage(JsonElement response, RequestLog log)
    {
        if (!response.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return;

        log.InputTokens = ReadInt64(usage, "input_tokens") ?? log.InputTokens;
        log.OutputTokens = ReadInt64(usage, "output_tokens") ?? log.OutputTokens;
        log.TotalTokens = ReadInt64(usage, "total_tokens") ?? log.TotalTokens;
    }

    private static void ApplyImageUsage(JsonElement response, RequestLog log)
    {
        if (!response.TryGetProperty("tool_usage", out var toolUsage) || toolUsage.ValueKind != JsonValueKind.Object)
            return;

        if (!toolUsage.TryGetProperty("image_gen", out var imageGen) || imageGen.ValueKind != JsonValueKind.Object)
            return;

        log.ImageInputTokens = ReadInt64(imageGen, "input_tokens") ?? log.ImageInputTokens;
        log.ImageOutputTokens = ReadInt64(imageGen, "output_tokens") ?? log.ImageOutputTokens;
        log.ImageTotalTokens = ReadInt64(imageGen, "total_tokens") ?? log.ImageTotalTokens;
    }

    private static long? ReadInt64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind != JsonValueKind.Number) return null;
        return value.TryGetInt64(out var result) ? result : null;
    }
}
