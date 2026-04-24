using System.Text;
using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using Xunit;

namespace ImageRelay.Api.Tests;

public class ResponseTokenUsageParserTests
{
    [Fact]
    public void Append_ExtractsStandardUsageFromCompletedEvent()
    {
        var log = new RequestLog();
        var parser = new ResponseTokenUsageParser();

        parser.Append(Encoding.UTF8.GetBytes(
            "event: response.completed\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"usage\":{\"input_tokens\":11,\"output_tokens\":22,\"total_tokens\":33}}}\n\n"), log);

        Assert.Equal(11, log.InputTokens);
        Assert.Equal(22, log.OutputTokens);
        Assert.Equal(33, log.TotalTokens);
    }

    [Fact]
    public void Append_ExtractsImageGenerationUsageFromCompletedEvent()
    {
        var log = new RequestLog();
        var parser = new ResponseTokenUsageParser();

        parser.Append(Encoding.UTF8.GetBytes(
            "event: response.completed\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"tool_usage\":{\"image_gen\":{\"input_tokens\":101,\"output_tokens\":202,\"total_tokens\":303}}}}\n\n"), log);

        Assert.Equal(101, log.ImageInputTokens);
        Assert.Equal(202, log.ImageOutputTokens);
        Assert.Equal(303, log.ImageTotalTokens);
    }

    [Fact]
    public void Append_IgnoresNullUsageMissingFieldsAndInvalidJson()
    {
        var log = new RequestLog();
        var parser = new ResponseTokenUsageParser();

        parser.Append(Encoding.UTF8.GetBytes("event: response.completed\ndata: {\"type\":\"response.completed\",\"response\":{\"usage\":null}}\n\n"), log);
        parser.Append(Encoding.UTF8.GetBytes("event: response.completed\ndata: {not-json}\n\n"), log);
        parser.Append(Encoding.UTF8.GetBytes("event: response.in_progress\ndata: {\"type\":\"response.in_progress\",\"response\":{\"usage\":{\"total_tokens\":99}}}\n\n"), log);

        Assert.Null(log.InputTokens);
        Assert.Null(log.OutputTokens);
        Assert.Null(log.TotalTokens);
        Assert.Null(log.ImageTotalTokens);
    }

    [Fact]
    public void Append_ParsesCompletedEventSplitAcrossChunks()
    {
        var log = new RequestLog();
        var parser = new ResponseTokenUsageParser();
        var sse =
            "event: response.completed\r\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"usage\":{\"input_tokens\":1,\"output_tokens\":2,\"total_tokens\":3},\"tool_usage\":{\"image_gen\":{\"input_tokens\":4,\"output_tokens\":5,\"total_tokens\":6}}}}\r\n\r\n";

        foreach (var chunk in new[] { sse[..7], sse[7..31], sse[31..96], sse[96..] })
        {
            parser.Append(Encoding.UTF8.GetBytes(chunk), log);
        }

        Assert.Equal(1, log.InputTokens);
        Assert.Equal(2, log.OutputTokens);
        Assert.Equal(3, log.TotalTokens);
        Assert.Equal(4, log.ImageInputTokens);
        Assert.Equal(5, log.ImageOutputTokens);
        Assert.Equal(6, log.ImageTotalTokens);
    }
}
