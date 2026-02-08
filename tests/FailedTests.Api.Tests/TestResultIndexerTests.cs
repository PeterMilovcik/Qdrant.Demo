using NUnit.Framework;
using Moq;
using OpenAI.Embeddings;
using Qdrant.Client;
using FailedTests.Api.Models;
using FailedTests.Api.Services;

namespace FailedTests.Api.Tests;

[TestFixture]
public class TestResultIndexerTests
{
    /// <summary>
    /// Verifies that <see cref="TestResultIndexer.IndexTestResultAsync"/>
    /// produces deterministic point and signature IDs for the same input.
    /// 
    /// We can't easily mock <see cref="EmbeddingClient"/> (sealed/no interface)
    /// and <see cref="QdrantClient"/> (gRPC-heavy), so we test the pure logic
    /// indirectly through <see cref="FailedTests.Api.Helpers.TextHelpers"/>.
    /// The full integration is validated in the Docker-based test.
    /// </summary>
    [Test]
    public void DeterministicIds_AreStableForSameInput()
    {
        var env = MakeEnvelope();
        var testName = FailedTests.Api.Helpers.TextHelpers.PickTestName(env.Result);

        var pointId1 = FailedTests.Api.Helpers.TextHelpers.DeterministicGuid(
            $"ado|{env.ProjectName}|{env.BuildId}|{env.TestRunId}|{env.Result.Id}");
        var pointId2 = FailedTests.Api.Helpers.TextHelpers.DeterministicGuid(
            $"ado|{env.ProjectName}|{env.BuildId}|{env.TestRunId}|{env.Result.Id}");

        Assert.That(pointId1, Is.EqualTo(pointId2));

        var sig1 = FailedTests.Api.Helpers.TextHelpers.DeterministicGuid(
            $"sig|{env.ProjectName}|{env.DefinitionName}|{testName}" +
            $"|{FailedTests.Api.Helpers.TextHelpers.Normalize(env.Result.ErrorMessage)}" +
            $"|{FailedTests.Api.Helpers.TextHelpers.NormalizeStack(env.Result.StackTrace)}");
        var sig2 = FailedTests.Api.Helpers.TextHelpers.DeterministicGuid(
            $"sig|{env.ProjectName}|{env.DefinitionName}|{testName}" +
            $"|{FailedTests.Api.Helpers.TextHelpers.Normalize(env.Result.ErrorMessage)}" +
            $"|{FailedTests.Api.Helpers.TextHelpers.NormalizeStack(env.Result.StackTrace)}");

        Assert.That(sig1, Is.EqualTo(sig2));
    }

    [Test]
    public void DifferentBuilds_ProduceDifferentPointIds_ButSameSignatureId()
    {
        var env1 = MakeEnvelope(buildId: 100);
        var env2 = MakeEnvelope(buildId: 200);

        var pointId1 = FailedTests.Api.Helpers.TextHelpers.DeterministicGuid(
            $"ado|{env1.ProjectName}|{env1.BuildId}|{env1.TestRunId}|{env1.Result.Id}");
        var pointId2 = FailedTests.Api.Helpers.TextHelpers.DeterministicGuid(
            $"ado|{env2.ProjectName}|{env2.BuildId}|{env2.TestRunId}|{env2.Result.Id}");

        // Different builds → different point IDs
        Assert.That(pointId1, Is.Not.EqualTo(pointId2));

        // Same test + error + stack → same signature
        var testName = FailedTests.Api.Helpers.TextHelpers.PickTestName(env1.Result);
        var sig1 = FailedTests.Api.Helpers.TextHelpers.DeterministicGuid(
            $"sig|{env1.ProjectName}|{env1.DefinitionName}|{testName}" +
            $"|{FailedTests.Api.Helpers.TextHelpers.Normalize(env1.Result.ErrorMessage)}" +
            $"|{FailedTests.Api.Helpers.TextHelpers.NormalizeStack(env1.Result.StackTrace)}");
        var sig2 = FailedTests.Api.Helpers.TextHelpers.DeterministicGuid(
            $"sig|{env2.ProjectName}|{env2.DefinitionName}|{testName}" +
            $"|{FailedTests.Api.Helpers.TextHelpers.Normalize(env2.Result.ErrorMessage)}" +
            $"|{FailedTests.Api.Helpers.TextHelpers.NormalizeStack(env2.Result.StackTrace)}");

        Assert.That(sig1, Is.EqualTo(sig2));
    }

    private static FailedTestEnvelope MakeEnvelope(
        int buildId = 1,
        int testRunId = 10,
        int resultId = 42)
    {
        return new FailedTestEnvelope(
            ProjectName: "TestProject",
            DefinitionName: "CI_Main",
            BuildId: buildId,
            BuildName: $"CI_Main_{buildId}",
            TestRunId: testRunId,
            Result: new AzureDevOpsTestCaseResultDto(
                Id: resultId,
                TestCaseTitle: "Should_calculate",
                AutomatedTestName: "My.Tests.CalcTests.Should_calculate",
                ComputerName: "agent-1",
                Outcome: "Failed",
                ErrorMessage: "System.NullReferenceException: Object reference not set",
                StackTrace: "at My.App.Calc.Add(Int32 a, Int32 b) in Calc.cs:line 10",
                StartedDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CompletedDate: new DateTime(2025, 1, 1, 0, 0, 2, DateTimeKind.Utc)));
    }
}
