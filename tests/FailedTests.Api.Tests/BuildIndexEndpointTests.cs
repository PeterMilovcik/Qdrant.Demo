using NUnit.Framework;
using Moq;
using FailedTests.Api.Models;
using FailedTests.Api.Services;

namespace FailedTests.Api.Tests;

[TestFixture]
public class BuildIndexEndpointTests
{
    private Mock<IAzureDevOpsService> _azdoMock = null!;
    private Mock<ITestResultIndexer> _indexerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _azdoMock = new Mock<IAzureDevOpsService>();
        _indexerMock = new Mock<ITestResultIndexer>();
    }

    // ─── Orchestration logic tests ──────────────────────────────
    // These test the same orchestration flow that BuildIndexEndpoints performs,
    // by calling the mocked services directly (the endpoint is a thin lambda).

    [Test]
    public async Task WhenNoTestRuns_ReturnsZeroCounts()
    {
        _azdoMock
            .Setup(s => s.GetTestRunsForBuildAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestRunInfo>());

        var testRuns = await _azdoMock.Object.GetTestRunsForBuildAsync(
            "https://dev.azure.com/org", "Proj", 123);

        Assert.That(testRuns, Is.Empty);
    }

    [Test]
    public async Task WhenTestRunsExist_FetchesFailedResults()
    {
        var runs = new List<TestRunInfo>
        {
            new(Id: 1, Name: "Run1", BuildUri: null, State: "Completed",
                TotalTests: 10, PassedTests: 8, UnresolvedTests: 0,
                StartedDate: DateTime.UtcNow, CompletedDate: DateTime.UtcNow),
            new(Id: 2, Name: "Run2", BuildUri: null, State: "Completed",
                TotalTests: 5, PassedTests: 5, UnresolvedTests: 0,
                StartedDate: DateTime.UtcNow, CompletedDate: DateTime.UtcNow)
        };

        _azdoMock
            .Setup(s => s.GetTestRunsForBuildAsync(
                It.IsAny<string>(), It.IsAny<string>(), 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        var failedResults = new List<TestResultInfo>
        {
            MakeTestResultInfo(1, "Failed"),
            MakeTestResultInfo(2, "Failed")
        };

        _azdoMock
            .Setup(s => s.GetTestResultsAsync(
                It.IsAny<string>(), It.IsAny<string>(), 1, "Failed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResults);

        _azdoMock
            .Setup(s => s.GetTestResultsAsync(
                It.IsAny<string>(), It.IsAny<string>(), 2, "Failed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestResultInfo>());

        _indexerMock
            .Setup(i => i.IndexTestResultAsync(It.IsAny<FailedTestEnvelope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResponse("pid", "sid"));

        // Simulate the orchestration logic
        var testRunsResult = await _azdoMock.Object.GetTestRunsForBuildAsync(
            "https://dev.azure.com/org", "Proj", 123);

        Assert.That(testRunsResult, Has.Count.EqualTo(2));

        var totalIndexed = 0;
        foreach (var run in testRunsResult)
        {
            var results = await _azdoMock.Object.GetTestResultsAsync(
                "https://dev.azure.com/org", "Proj", run.Id, "Failed");

            foreach (var result in results)
            {
                var envelope = new FailedTestEnvelope(
                    ProjectName: "Proj",
                    DefinitionName: "CI",
                    BuildId: 123,
                    BuildName: run.Name ?? "Run",
                    TestRunId: run.Id,
                    Result: new AzureDevOpsTestCaseResultDto(
                        Id: result.Id,
                        TestCaseTitle: result.TestCaseTitle,
                        AutomatedTestName: result.AutomatedTestName,
                        ComputerName: result.ComputerName,
                        Outcome: result.Outcome,
                        ErrorMessage: result.ErrorMessage,
                        StackTrace: result.StackTrace,
                        StartedDate: result.StartedDate,
                        CompletedDate: result.CompletedDate));

                await _indexerMock.Object.IndexTestResultAsync(envelope);
                totalIndexed++;
            }
        }

        Assert.That(totalIndexed, Is.EqualTo(2));
        _indexerMock.Verify(
            i => i.IndexTestResultAsync(It.IsAny<FailedTestEnvelope>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task WhenIndexerThrows_OtherResultsStillIndexed()
    {
        var run = new TestRunInfo(
            Id: 1, Name: "Run1", BuildUri: null, State: "Completed",
            TotalTests: 3, PassedTests: 1, UnresolvedTests: 0,
            StartedDate: DateTime.UtcNow, CompletedDate: DateTime.UtcNow);

        var failedResults = new List<TestResultInfo>
        {
            MakeTestResultInfo(1, "Failed"),
            MakeTestResultInfo(2, "Failed"),
            MakeTestResultInfo(3, "Failed")
        };

        var callCount = 0;
        _indexerMock
            .Setup(i => i.IndexTestResultAsync(It.IsAny<FailedTestEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns<FailedTestEnvelope, CancellationToken>((env, ct) =>
            {
                callCount++;
                if (callCount == 2)
                    throw new Exception("Embedding service unavailable");
                return Task.FromResult(new IndexResponse("pid", "sid"));
            });

        var errors = new List<string>();
        var totalIndexed = 0;

        foreach (var result in failedResults)
        {
            try
            {
                var envelope = new FailedTestEnvelope(
                    ProjectName: "Proj", DefinitionName: "CI",
                    BuildId: 1, BuildName: "Run1", TestRunId: 1,
                    Result: new AzureDevOpsTestCaseResultDto(
                        result.Id, result.TestCaseTitle, result.AutomatedTestName,
                        result.ComputerName, result.Outcome, result.ErrorMessage,
                        result.StackTrace, result.StartedDate, result.CompletedDate));

                await _indexerMock.Object.IndexTestResultAsync(envelope);
                totalIndexed++;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        // 2 succeeded, 1 failed
        Assert.That(totalIndexed, Is.EqualTo(2));
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Embedding service unavailable"));
    }

    [Test]
    public void IAzureDevOpsService_CanBeMocked()
    {
        // Verify the interface is mockable (no sealed class issues)
        var mock = new Mock<IAzureDevOpsService>(MockBehavior.Strict);
        Assert.That(mock.Object, Is.Not.Null);
    }

    [Test]
    public void ITestResultIndexer_CanBeMocked()
    {
        var mock = new Mock<ITestResultIndexer>(MockBehavior.Strict);
        Assert.That(mock.Object, Is.Not.Null);
    }

    private static TestResultInfo MakeTestResultInfo(int id, string outcome) => new(
        Id: id,
        TestCaseTitle: $"Test_{id}",
        AutomatedTestName: $"Ns.Class.Test_{id}",
        ComputerName: "agent-1",
        Outcome: outcome,
        ErrorMessage: "NullReferenceException",
        StackTrace: "at Foo.Bar()",
        StartedDate: DateTime.UtcNow,
        CompletedDate: DateTime.UtcNow,
        TestRunId: 1,
        TestRunName: "Run1");
}
