using System.Globalization;
using System.Text.Json;

namespace ClaudeSessions.Core;

/// <summary>Pure JSON parsing and status mapping (JsonDocument only: no reflection, trim/AOT safe).</summary>
public static class SessionParser
{
    public static SessionStatus ParseStatus(string? value) => value switch
    {
        "busy" => SessionStatus.Busy,
        "shell" => SessionStatus.Shell,
        "waiting" => SessionStatus.Waiting,
        _ => SessionStatus.Idle,
    };

    /// <summary>Returns false for partially written / malformed JSON or a file without a usable pid.</summary>
    public static bool TryParse(string json, out SessionRecord? record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var pid = GetLong(root, "pid");
            if (pid is null or <= 0 or > int.MaxValue)
            {
                return false;
            }

            var statusUpdatedAt = ValidTimestamp(GetLong(root, "statusUpdatedAt") ?? GetLong(root, "updatedAt"));
            var waitingFor = GetString(root, "waitingFor");

            record = new SessionRecord(
                (int)pid.Value,
                GetString(root, "sessionId") ?? string.Empty,
                GetString(root, "cwd") ?? string.Empty,
                ValidTimestamp(GetLong(root, "startedAt")),
                GetLong(root, "procStart") ?? 0,
                GetString(root, "kind") ?? string.Empty,
                GetString(root, "name") ?? string.Empty,
                ParseStatus(GetString(root, "status")),
                string.IsNullOrWhiteSpace(waitingFor) ? null : waitingFor,
                statusUpdatedAt);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly long MinMs = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();
    private static readonly long MaxMs = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    /// <summary>Unix ms timestamp, or 0 when missing, negative or outside the DateTimeOffset range.</summary>
    internal static long ValidTimestamp(long? ms) => ms is > 0 && ms.Value >= MinMs && ms.Value <= MaxMs ? ms.Value : 0;

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    /// <summary>Reads a number, or a string holding a number (procStart is a string in the file).</summary>
    private static long? GetLong(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p))
        {
            return null;
        }

        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null,
        };
    }
}
