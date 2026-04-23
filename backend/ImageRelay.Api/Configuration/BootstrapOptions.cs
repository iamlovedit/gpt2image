namespace ImageRelay.Api.Configuration;

public class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "";
}
