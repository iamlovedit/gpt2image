using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using Xunit;

namespace ImageRelay.Api.Tests;

public class UpstreamForwarderTests
{
    [Fact]
    public void ResolveNoLeaseFailure_WhenNoAccountWasAttempted_Returns503()
    {
        var failure = UpstreamForwarder.ResolveNoLeaseFailure(false, null);

        Assert.Equal(503, failure.HttpStatus);
        Assert.Equal("no available upstream account", failure.Message);
        Assert.Equal(RequestBusinessStatus.NoAvailableAccount, failure.BusinessStatus);
        Assert.Equal("NoHealthyAccount", failure.ErrorType);
    }

    [Fact]
    public void ResolveNoLeaseFailure_WhenAccountsWereAttempted_Returns502()
    {
        var failure = UpstreamForwarder.ResolveNoLeaseFailure(true, null);

        Assert.Equal(502, failure.HttpStatus);
        Assert.Equal("all upstream attempts failed", failure.Message);
        Assert.Equal(RequestBusinessStatus.UpstreamError, failure.BusinessStatus);
        Assert.Equal("ExhaustedHealthyAccounts", failure.ErrorType);
    }

    [Fact]
    public void ResolveNoLeaseFailure_WhenAccountsWereAttempted_PreservesExistingErrorType()
    {
        var failure = UpstreamForwarder.ResolveNoLeaseFailure(true, "UpstreamError");

        Assert.Equal(502, failure.HttpStatus);
        Assert.Equal("UpstreamError", failure.ErrorType);
    }
}
