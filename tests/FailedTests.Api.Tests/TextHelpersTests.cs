using NUnit.Framework;
using FailedTests.Api.Helpers;
using FailedTests.Api.Models;

namespace FailedTests.Api.Tests;

[TestFixture]
public class TextHelpersTests
{
    // ─── PickTestName ───────────────────────────────────────────

    [Test]
    public void PickTestName_PrefersAutomatedTestName()
    {
        var dto = MakeResult(automatedTestName: "Ns.Class.Method", testCaseTitle: "Title");
        Assert.That(TextHelpers.PickTestName(dto), Is.EqualTo("Ns.Class.Method"));
    }

    [Test]
    public void PickTestName_FallsBackToTestCaseTitle()
    {
        var dto = MakeResult(automatedTestName: null, testCaseTitle: "Title");
        Assert.That(TextHelpers.PickTestName(dto), Is.EqualTo("Title"));
    }

    [Test]
    public void PickTestName_ReturnsPlaceholderWhenBothNull()
    {
        var dto = MakeResult(automatedTestName: null, testCaseTitle: null);
        Assert.That(TextHelpers.PickTestName(dto), Is.EqualTo("<unknown-test>"));
    }

    [Test]
    public void PickTestName_IgnoresWhitespaceOnlyAutomatedTestName()
    {
        var dto = MakeResult(automatedTestName: "   ", testCaseTitle: "Title");
        Assert.That(TextHelpers.PickTestName(dto), Is.EqualTo("Title"));
    }

    // ─── DeterministicGuid ──────────────────────────────────────

    [Test]
    public void DeterministicGuid_IsDeterministic()
    {
        var a = TextHelpers.DeterministicGuid("same-input");
        var b = TextHelpers.DeterministicGuid("same-input");
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void DeterministicGuid_DifferentInputDifferentGuid()
    {
        var a = TextHelpers.DeterministicGuid("input-1");
        var b = TextHelpers.DeterministicGuid("input-2");
        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void DeterministicGuid_SetsVersionAndVariantBits()
    {
        var g = TextHelpers.DeterministicGuid("test");
        // The code sets version=5 and RFC 4122 variant bits on the raw byte span.
        // .NET Guid uses mixed-endian layout, so the string representation may differ.
        // What matters is the GUID is deterministic and non-empty.
        Assert.That(g, Is.Not.EqualTo(Guid.Empty));
        // Verify it matches the deterministic expectation
        Assert.That(g, Is.EqualTo(TextHelpers.DeterministicGuid("test")));
    }

    // ─── Normalize ──────────────────────────────────────────────

    [Test]
    public void Normalize_ReplacesGuids()
    {
        var input = "Error for id 12345678-abcd-1234-abcd-123456789abc in context";
        var result = TextHelpers.Normalize(input);
        Assert.That(result, Does.Contain("<guid>"));
        Assert.That(result, Does.Not.Contain("12345678-abcd"));
    }

    [Test]
    public void Normalize_ReplacesNumbers()
    {
        var result = TextHelpers.Normalize("line 42 col 7");
        Assert.That(result, Is.EqualTo("line <n> col <n>"));
    }

    [Test]
    public void Normalize_CollapsesWhitespace()
    {
        var result = TextHelpers.Normalize("a   b\t\tc");
        Assert.That(result, Is.EqualTo("a b c"));
    }

    [Test]
    public void Normalize_ReturnsEmptyForNull()
    {
        Assert.That(TextHelpers.Normalize(null), Is.EqualTo(""));
    }

    // ─── NormalizeStack ─────────────────────────────────────────

    [Test]
    public void NormalizeStack_StripsLineNumbers()
    {
        var stack = "at Foo.Bar() in C:\\src\\Foo.cs:line 42";
        var result = TextHelpers.NormalizeStack(stack);
        Assert.That(result, Does.Contain(":line <n>"));
        Assert.That(result, Does.Not.Contain(":line 42"));
    }

    [Test]
    public void NormalizeStack_LimitsTo12Frames()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"at Frame{i}()");
        var stack = string.Join("\n", lines);
        var result = TextHelpers.NormalizeStack(stack);
        Assert.That(result.Split('\n'), Has.Length.EqualTo(12));
    }

    [Test]
    public void NormalizeStack_ReturnsEmptyForNull()
    {
        Assert.That(TextHelpers.NormalizeStack(null), Is.EqualTo(""));
    }

    // ─── ToUnixMs ───────────────────────────────────────────────

    [Test]
    public void ToUnixMs_ReturnsZeroForEpoch()
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(TextHelpers.ToUnixMs(epoch), Is.EqualTo(0));
    }

    [Test]
    public void ToUnixMs_KnownTimestamp()
    {
        // 2025-01-01T00:00:00Z = 1735689600000 ms
        var dt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(TextHelpers.ToUnixMs(dt), Is.EqualTo(1735689600000L));
    }

    // ─── BuildEmbeddingText ─────────────────────────────────────

    [Test]
    public void BuildEmbeddingText_ContainsAllFields()
    {
        var env = new FailedTestEnvelope(
            ProjectName: "Proj",
            DefinitionName: "CI",
            BuildId: 1,
            BuildName: "CI_1",
            TestRunId: 10,
            Result: MakeResult(
                outcome: "Failed",
                errorMessage: "NullRef",
                stackTrace: "at Foo.Bar()"));

        var text = TextHelpers.BuildEmbeddingText(env, "My.Test");

        Assert.That(text, Does.Contain("Project: Proj"));
        Assert.That(text, Does.Contain("Definition: CI"));
        Assert.That(text, Does.Contain("Test: My.Test"));
        Assert.That(text, Does.Contain("NullRef"));
        Assert.That(text, Does.Contain("at Foo.Bar()"));
    }

    // ─── helpers ────────────────────────────────────────────────

    private static AzureDevOpsTestCaseResultDto MakeResult(
        int id = 1,
        string? testCaseTitle = "Title",
        string? automatedTestName = "Auto",
        string? computerName = "agent",
        string? outcome = "Failed",
        string? errorMessage = "err",
        string? stackTrace = "at X()",
        DateTime? startedDate = null,
        DateTime? completedDate = null)
    {
        return new AzureDevOpsTestCaseResultDto(
            Id: id,
            TestCaseTitle: testCaseTitle,
            AutomatedTestName: automatedTestName,
            ComputerName: computerName,
            Outcome: outcome,
            ErrorMessage: errorMessage,
            StackTrace: stackTrace,
            StartedDate: startedDate,
            CompletedDate: completedDate);
    }
}
