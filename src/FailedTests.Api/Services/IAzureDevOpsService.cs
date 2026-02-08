using FailedTests.Api.Models;

namespace FailedTests.Api.Services;

/// <summary>
/// Abstraction over Azure DevOps Test Management APIs.
/// Method signatures mirror the SDK (<c>TestManagementHttpClient</c>) but use
/// our own domain records so callers never depend on the SDK.
/// </summary>
public interface IAzureDevOpsService
{
    /// <summary>
    /// Retrieve all test runs associated with a given build.
    /// Maps to <c>TestManagementHttpClient.GetTestRunsAsync(project, buildUri)</c>.
    /// </summary>
    Task<IReadOnlyList<TestRunInfo>> GetTestRunsForBuildAsync(
        string collectionUrl, string project, int buildId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve test results for a specific test run, optionally filtered by outcome.
    /// Maps to <c>TestManagementHttpClient.GetTestResultsAsync(project, runId)</c>.
    /// </summary>
    Task<IReadOnlyList<TestResultInfo>> GetTestResultsAsync(
        string collectionUrl, string project, int testRunId,
        string? outcomeFilter = null,
        CancellationToken ct = default);
}
