namespace ImageRelay.Api.Features.UpstreamAccounts;

public record AccountImportPayload(List<AccountImportItem> Items, ImportDuplicateStrategy Strategy, bool IsValidShape);

public record AccountImportItem(
    string? AccessToken,
    string? RefreshToken,
    string? ChatGptAccountId,
    DateTime? AccessTokenExpiresAt,
    int? ConcurrencyLimit,
    string? Notes,
    string? Name,
    string? Email,
    string? Platform,
    string? AccountType,
    string? ProxyKey,
    int? Priority,
    decimal? RateMultiplier,
    bool? AutoPauseOnExpired,
    string? ChatGptUserId,
    string? ClientId,
    string? OrganizationId,
    string? PlanType,
    DateTime? SubscriptionExpiresAt,
    string? RawMetadataJson);

public static class AccountImportParser
{
    public static AccountImportPayload NormalizeImportItems(JsonElement root)
    {
        var strategy = ReadStrategy(root);
        var itemsRoot = ResolveItemsRoot(root);
        if (itemsRoot.ValueKind != JsonValueKind.Array)
            return new AccountImportPayload([], strategy, false);

        var items = itemsRoot.EnumerateArray()
            .Select(ReadAccountImportItem)
            .ToList();

        return new AccountImportPayload(items, strategy, true);
    }

    public static AccountImportItem ReadAccountImportItem(JsonElement item)
    {
        var credentials = ReadObject(item, "credentials");
        var accessExpiresAt = ReadDateTime(credentials, "expires_at")
            ?? ReadDateTime(item, "accessTokenExpiresAt", "access_token_expires_at")
            ?? ReadDateTime(item, "expires_at");
        var email = ReadString(credentials, "email") ?? ReadString(item, "email");
        var name = ReadString(item, "name");
        var notes = ReadString(item, "notes") ?? email ?? name;

        return new AccountImportItem(
            ReadString(credentials, "access_token", "accessToken") ?? ReadString(item, "accessToken", "access_token"),
            ReadString(credentials, "refresh_token", "refreshToken") ?? ReadString(item, "refreshToken", "refresh_token"),
            ReadString(credentials, "chatgpt_account_id", "chatGptAccountId", "chatgptAccountId")
                ?? ReadString(item, "chatGptAccountId", "chatgptAccountId", "chatgpt_account_id"),
            accessExpiresAt,
            ReadInt(item, "concurrency", "concurrencyLimit", "concurrency_limit"),
            notes,
            name,
            email,
            ReadString(item, "platform"),
            ReadString(item, "type", "accountType", "account_type"),
            ReadString(item, "proxy_key", "proxyKey"),
            ReadInt(item, "priority"),
            ReadDecimal(item, "rate_multiplier", "rateMultiplier"),
            ReadBool(item, "auto_pause_on_expired", "autoPauseOnExpired"),
            ReadString(credentials, "chatgpt_user_id", "chatGptUserId", "chatgptUserId")
                ?? ReadString(item, "chatGptUserId", "chatgptUserId", "chatgpt_user_id"),
            ReadString(credentials, "client_id", "clientId")
                ?? ReadString(item, "clientId", "client_id"),
            ReadString(credentials, "organization_id", "organizationId")
                ?? ReadString(item, "organizationId", "organization_id"),
            ReadString(credentials, "plan_type", "planType")
                ?? ReadString(item, "planType", "plan_type"),
            ReadDateTime(credentials, "subscription_expires_at", "subscriptionExpiresAt")
                ?? ReadDateTime(item, "subscriptionExpiresAt", "subscription_expires_at"),
            BuildRawMetadataJson(item, credentials));
    }

    private static ImportDuplicateStrategy ReadStrategy(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !TryGetProperty(root, "strategy", out var prop))
            return ImportDuplicateStrategy.Skip;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)
            && Enum.IsDefined(typeof(ImportDuplicateStrategy), value))
            return (ImportDuplicateStrategy)value;

        if (prop.ValueKind == JsonValueKind.String
            && Enum.TryParse<ImportDuplicateStrategy>(prop.GetString(), ignoreCase: true, out var parsed))
            return parsed;

        return ImportDuplicateStrategy.Skip;
    }

    private static JsonElement ResolveItemsRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind != JsonValueKind.Object) return default;
        if (TryGetProperty(root, "items", out var items)) return items;
        if (TryGetProperty(root, "accounts", out var accounts)) return accounts;
        return default;
    }

    private static JsonElement ReadObject(JsonElement item, string name)
    {
        return TryGetProperty(item, name, out var prop) && prop.ValueKind == JsonValueKind.Object ? prop : default;
    }

    private static string? ReadString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.String) return NormalizeOptional(prop.GetString());
            if (prop.ValueKind == JsonValueKind.Number || prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                return NormalizeOptional(prop.ToString());
            if (prop.ValueKind == JsonValueKind.Null) return null;
        }

        return null;
    }

    private static int? ReadInt(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)) return value;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value)) return value;
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var value)) return value;
            if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out value)) return value;
        }

        return null;
    }

    private static bool? ReadBool(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
            if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var value)) return value;
        }

        return null;
    }

    private static DateTime? ReadDateTime(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var unixSeconds))
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            if (prop.ValueKind == JsonValueKind.String)
            {
                var text = prop.GetString();
                if (long.TryParse(text, out unixSeconds))
                    return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                if (DateTimeOffset.TryParse(text, out var dto))
                    return dto.UtcDateTime;
            }
        }

        return null;
    }

    private static string? BuildRawMetadataJson(JsonElement item, JsonElement credentials)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteIfPresent(writer, item, "extra", "extra");
            WriteIfPresent(writer, credentials, "model_mapping", "model_mapping");
            WriteIfPresent(writer, credentials, "_token_version", "_token_version");
            WriteIfPresent(writer, credentials, "id_token", "id_token");
            writer.WriteEndObject();
        }

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        return json == "{}" ? null : json;
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, JsonElement item, string sourceName, string outputName)
    {
        if (!TryGetProperty(item, sourceName, out var prop) || prop.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return;

        writer.WritePropertyName(outputName);
        prop.WriteTo(writer);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryGetProperty(JsonElement item, string name, out JsonElement value)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in item.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
