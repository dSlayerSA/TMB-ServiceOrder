using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OrderManagement.Tests.Golden;

/// <summary>
/// Minimal golden-file (approval) helper: serializes a value, replaces the
/// values that legitimately change on every run (ids and timestamps) with
/// stable placeholders, and compares the result against a committed snapshot.
///
/// What this locks down is the *contract*: property names and casing, nesting,
/// JSON types, enum representation (name vs number) and ordering - the things
/// a consumer (the frontend, or the Worker reading an event) would break on.
/// A mismatch writes a ".received.json" next to the snapshot so the diff can be
/// reviewed and, if intended, promoted by overwriting the ".verified.json".
/// </summary>
public static class JsonGolden
{
    private static readonly Regex GuidPattern = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    private static readonly Regex TimestampPattern = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}",
        RegexOptions.Compiled);

    /// <summary>Serializer settings matching what ASP.NET Core uses for HTTP responses.</summary>
    public static readonly JsonSerializerOptions WebContract =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Serializer settings matching the raw <see cref="JsonSerializer.Serialize{T}(T, JsonSerializerOptions?)"/>
    /// calls used for the broker payloads (no naming policy, so PascalCase).
    /// </summary>
    public static readonly JsonSerializerOptions MessagingContract =
        new() { WriteIndented = true };

    public static void Verify(
        string name,
        object value,
        JsonSerializerOptions options,
        [CallerFilePath] string callerFilePath = "")
    {
        var json = JsonSerializer.Serialize(value, options);
        VerifyJson(name, json, callerFilePath);
    }

    /// <summary>Verifies an already-serialized payload, e.g. a raw HTTP response body.</summary>
    public static void VerifyJson(
        string name,
        string json,
        [CallerFilePath] string callerFilePath = "")
    {
        var received = Normalize(json);
        var directory = Path.Combine(Path.GetDirectoryName(callerFilePath)!, "Snapshots");
        Directory.CreateDirectory(directory);

        var verifiedPath = Path.Combine(directory, $"{name}.verified.json");
        var receivedPath = Path.Combine(directory, $"{name}.received.json");

        if (!File.Exists(verifiedPath))
        {
            File.WriteAllText(receivedPath, received);
            Assert.Fail(
                $"No golden snapshot for '{name}'. Review '{receivedPath}' and, if correct, " +
                $"rename it to '{Path.GetFileName(verifiedPath)}'.");
        }

        var verified = Normalize(File.ReadAllText(verifiedPath));
        if (verified == received)
        {
            if (File.Exists(receivedPath))
                File.Delete(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, received);
        Assert.Fail(
            $"Contract '{name}' changed.{Environment.NewLine}" +
            $"--- expected ---{Environment.NewLine}{verified}{Environment.NewLine}" +
            $"--- actual ---{Environment.NewLine}{received}{Environment.NewLine}" +
            $"If the change is intended, replace '{Path.GetFileName(verifiedPath)}' with '{Path.GetFileName(receivedPath)}'.");
    }

    /// <summary>
    /// Re-serializes with a fixed layout and scrubs volatile values, so the
    /// comparison is insensitive to whitespace and to run-specific ids/clocks.
    /// </summary>
    private static string Normalize(string json)
    {
        var node = JsonNode.Parse(json);
        Scrub(node);
        return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .ReplaceLineEndings("\n") ?? "null";
    }

    private static void Scrub(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (TryScrubScalar(obj[key], out var replacement))
                        obj[key] = replacement;
                    else
                        Scrub(obj[key]);
                }
                break;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (TryScrubScalar(array[i], out var replacement))
                        array[i] = replacement;
                    else
                        Scrub(array[i]);
                }
                break;
        }
    }

    /// <summary>
    /// Only scalars are ever replaced. Re-assigning an object or array back into
    /// the slot it already occupies throws ("the node already has a parent"), so
    /// containers are recursed into in place instead.
    /// </summary>
    private static bool TryScrubScalar(JsonNode? node, out JsonNode? replacement)
    {
        replacement = null;

        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
            return false;

        if (GuidPattern.IsMatch(text))
            replacement = JsonValue.Create("{scrubbed-guid}");
        else if (TimestampPattern.IsMatch(text))
            replacement = JsonValue.Create("{scrubbed-timestamp}");

        return replacement is not null;
    }
}
