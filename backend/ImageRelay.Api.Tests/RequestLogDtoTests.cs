using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Features.Logs;
using Xunit;

namespace ImageRelay.Api.Tests;

public class RequestLogDtoTests
{
    [Fact]
    public void From_IncludesClientKeyNameAndPreservesLogFields()
    {
        var clientKeyId = Guid.NewGuid();
        var log = new RequestLog
        {
            Id = Guid.NewGuid(),
            RequestId = "req-1",
            ClientKeyId = clientKeyId,
            ExternalModel = "gpt-5.4",
            BusinessStatus = RequestBusinessStatus.Success,
            TotalTokens = 123
        };

        var dto = RequestLogDto.From(log, "Production Key");

        Assert.Equal(log.Id, dto.Id);
        Assert.Equal("req-1", dto.RequestId);
        Assert.Equal(clientKeyId, dto.ClientKeyId);
        Assert.Equal("Production Key", dto.ClientKeyName);
        Assert.Equal("gpt-5.4", dto.ExternalModel);
        Assert.Equal(RequestBusinessStatus.Success, dto.BusinessStatus);
        Assert.Equal(123, dto.TotalTokens);
    }
}
