namespace ImageRelay.Api.Configuration;

public class UpstreamOptions
{
    public const string SectionName = "Upstream";

    public string BaseUrl { get; set; } = "https://chatgpt.com";
    public string ResponsesPath { get; set; } = "/backend-api/codex/responses";
    public string TokenUrl { get; set; } = "https://auth.openai.com/oauth/token";
    public string TokenClientId { get; set; } = "";

    public string ResponsesUrl => BaseUrl.TrimEnd('/') + ResponsesPath;
}
