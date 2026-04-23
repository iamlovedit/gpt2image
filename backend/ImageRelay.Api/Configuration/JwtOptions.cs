namespace ImageRelay.Api.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "image-relay";
    public string Audience { get; set; } = "image-relay-admin";
    public string Secret { get; set; } = "";
    public int ExpiresMinutes { get; set; } = 60;
}
