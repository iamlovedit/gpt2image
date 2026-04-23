using System.Text.Json;

namespace ImageRelay.Api.Tests;

internal static class RootRequestFixtureLoader
{
    public static string LoadRequestBody(string fixtureFileName)
    {
        var fixturePath = FindFixturePath(fixtureFileName);
        var raw = File.ReadAllText(fixturePath);

        using var doc = JsonDocument.Parse(raw);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("body", out var bodyElement)
            && bodyElement.ValueKind == JsonValueKind.Object)
        {
            return bodyElement.GetRawText();
        }

        return doc.RootElement.GetRawText();
    }

    private static string FindFixturePath(string fixtureFileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fixtureFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate fixture '{fixtureFileName}' from '{AppContext.BaseDirectory}'.");
    }
}
