using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FailedTests.Api.Models;

namespace FailedTests.Api.Helpers;

/// <summary>
/// Pure helper methods for text normalisation, embedding text construction,
/// deterministic UUID generation, and timestamp conversion.
/// </summary>
public static class TextHelpers
{
    /// <summary>Pick the best available human-readable test name.</summary>
    public static string PickTestName(AzureDevOpsTestCaseResultDto r)
        => !string.IsNullOrWhiteSpace(r.AutomatedTestName) ? r.AutomatedTestName!
         : !string.IsNullOrWhiteSpace(r.TestCaseTitle)     ? r.TestCaseTitle!
         : "<unknown-test>";

    /// <summary>
    /// Build the text that gets embedded. This determines what "similarity" means —
    /// here we include project context + error + stack.
    /// </summary>
    public static string BuildEmbeddingText(FailedTestEnvelope env, string testName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Project: {env.ProjectName}");
        sb.AppendLine($"Definition: {env.DefinitionName}");
        sb.AppendLine($"Build: {env.BuildName} ({env.BuildId})");
        sb.AppendLine($"Test: {testName}");
        sb.AppendLine($"Outcome: {env.Result.Outcome}");
        sb.AppendLine();
        sb.AppendLine(env.Result.ErrorMessage ?? "");
        sb.AppendLine();
        sb.AppendLine(env.Result.StackTrace ?? "");
        return sb.ToString();
    }

    /// <summary>
    /// Deterministic UUID from a string (SHA-256 → 16 bytes → RFC 4122 variant + v5-like version bits).
    /// </summary>
    public static Guid DeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        Span<byte> g = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(g);

        // version 5 (0101xxxx)
        g[6] = (byte)((g[6] & 0x0F) | 0x50);
        // RFC 4122 variant (10xxxxxx)
        g[8] = (byte)((g[8] & 0x3F) | 0x80);

        return new Guid(g);
    }

    /// <summary>Convert a DateTime to Unix epoch milliseconds.</summary>
    public static long ToUnixMs(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;
    }

    /// <summary>
    /// Strip volatile tokens (GUIDs, numbers, excess whitespace) from an error message
    /// so that the deterministic signature is stable across runs.
    /// </summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var text = s.Trim();

        text = Regex.Replace(text,
            @"\b[0-9a-fA-F]{8}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{12}\b",
            "<guid>");
        text = Regex.Replace(text, @"\b\d+\b", "<n>");
        text = Regex.Replace(text, @"\s+", " ");

        return text;
    }

    /// <summary>
    /// Keep only the top 12 stack frames and strip volatile line numbers,
    /// producing a stable fingerprint for the signature hash.
    /// </summary>
    public static string NormalizeStack(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Select(l => l.Trim())
                     .Select(l => Regex.Replace(l, @":line\s+\d+", ":line <n>"))
                     .Take(12);
        return string.Join("\n", lines);
    }
}
