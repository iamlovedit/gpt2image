using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Features.Dashboard;
using Xunit;

namespace ImageRelay.Api.Tests;

public class RequestStatsServiceTests
{
    [Fact]
    public void CreateRange_TodayUsesHourlyBucketsFromLocalMidnight()
    {
        var nowUtc = new DateTime(2026, 4, 24, 10, 30, 0, DateTimeKind.Utc);

        var range = RequestStatsService.CreateRange("today", nowUtc);

        Assert.NotNull(range);
        Assert.Equal("today", range.Range);
        Assert.Equal("hour", range.BucketUnit);
        Assert.Equal(19, range.BucketCount);
        Assert.Equal(new DateTime(2026, 4, 23, 16, 0, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.Equal(nowUtc, range.EndUtc);
    }

    [Theory]
    [InlineData("7day", "7d", 7, "day")]
    [InlineData("30day", "30d", 30, "day")]
    public void CreateRange_NormalizesDayRanges(string input, string expectedRange, int expectedBucketCount, string expectedBucketUnit)
    {
        var nowUtc = new DateTime(2026, 4, 24, 10, 30, 0, DateTimeKind.Utc);

        var range = RequestStatsService.CreateRange(input, nowUtc);

        Assert.NotNull(range);
        Assert.Equal(expectedRange, range.Range);
        Assert.Equal(expectedBucketUnit, range.BucketUnit);
        Assert.Equal(expectedBucketCount, range.BucketCount);
    }

    [Fact]
    public void Build_ComputesBucketsBreakdownsAndTokenTotals()
    {
        var accountId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var range = new RequestStatsRange(
            "today",
            "hour",
            new DateTime(2026, 4, 23, 16, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 23, 18, 30, 0, DateTimeKind.Utc),
            3);
        var logs = new[]
        {
            new RequestLog
            {
                StartedAt = new DateTime(2026, 4, 23, 16, 10, 0, DateTimeKind.Utc),
                BusinessStatus = RequestBusinessStatus.Success,
                DurationMs = 100,
                InputTokens = 10,
                OutputTokens = 20,
                TotalTokens = 30,
                ImageTotalTokens = 40,
                UpstreamAccountId = accountId,
                ClientKeyId = keyId
            },
            new RequestLog
            {
                StartedAt = new DateTime(2026, 4, 23, 16, 50, 0, DateTimeKind.Utc),
                BusinessStatus = RequestBusinessStatus.UpstreamError,
                DurationMs = 300,
                ErrorType = "upstream_timeout",
                InputTokens = 1,
                OutputTokens = 2,
                TotalTokens = 3,
                ImageTotalTokens = 4,
                UpstreamAccountId = accountId,
                ClientKeyId = keyId
            },
            new RequestLog
            {
                StartedAt = new DateTime(2026, 4, 23, 17, 5, 0, DateTimeKind.Utc),
                BusinessStatus = RequestBusinessStatus.ClientError,
                ErrorType = "",
                UpstreamAccountId = null,
                ClientKeyId = null
            }
        };

        var response = RequestStatsService.Build(
            range,
            logs,
            new Dictionary<Guid, string> { [accountId] = "Account A" },
            new Dictionary<Guid, string> { [keyId] = "Client Key A" });

        Assert.Equal("today", response.Range);
        Assert.Equal("hour", response.BucketUnit);
        Assert.Equal(3, response.Series.Count);
        Assert.Equal(2, response.Series[0].RequestCount);
        Assert.Equal(1, response.Series[0].SuccessCount);
        Assert.Equal(1, response.Series[0].FailureCount);
        Assert.Equal(0.5, response.Series[0].SuccessRate);
        Assert.Equal(200, response.Series[0].AvgDurationMs);
        Assert.Equal(11, response.Series[0].InputTokens);
        Assert.Equal(22, response.Series[0].OutputTokens);
        Assert.Equal(33, response.Series[0].TotalTokens);
        Assert.Equal(44, response.Series[0].ImageTotalTokens);
        Assert.Equal(0, response.Series[2].RequestCount);
        Assert.Null(response.Series[2].SuccessRate);
        Assert.Contains(response.StatusBreakdown, item => item.Status == RequestBusinessStatus.Success && item.Count == 1);
        Assert.Contains(response.ErrorTypeBreakdown, item => item.ErrorType == "upstream_timeout" && item.Count == 1);
        Assert.Contains(response.ErrorTypeBreakdown, item => item.ErrorType == "Unknown" && item.Count == 1);
        Assert.Contains(response.AccountBreakdown, item => item.Name == "Account A" && item.RequestCount == 2);
        Assert.Contains(response.AccountBreakdown, item => item.Name == "未分配账号" && item.RequestCount == 1);
        Assert.Contains(response.KeyBreakdown, item => item.Name == "Client Key A" && item.RequestCount == 2 && item.SuccessCount == 1 && item.FailureCount == 1);
        Assert.Contains(response.KeyBreakdown, item => item.Name == "未识别 Key" && item.RequestCount == 1 && item.FailureCount == 1);
    }
}
