using FailedTests.Api.Models;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace FailedTests.Api.Services;

/// <summary>
/// Production implementation of <see cref="IAzureDevOpsService"/>.
/// Uses the Azure DevOps .NET SDK (<c>VssConnection</c> + <c>TestManagementHttpClient</c>).
/// PAT is read from the <c>AZURE_DEVOPS_PAT</c> environment variable.
/// </summary>
public sealed class AzureDevOpsService : IAzureDevOpsService
{
    private readonly string _pat;

    public AzureDevOpsService()
    {
        _pat = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT")
            ?? throw new InvalidOperationException(
                "AZURE_DEVOPS_PAT environment variable is not set.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestRunInfo>> GetTestRunsForBuildAsync(
        string collectionUrl, string project, int buildId,
        CancellationToken ct = default)
    {
        using var connection = CreateConnection(collectionUrl);
        var client = connection.GetClient<TestManagementHttpClient>();

        // The SDK filters test runs by buildUri, which follows the vstfs:// scheme.
        var buildUri = $"vstfs:///Build/Build/{buildId}";

        var runs = await client.GetTestRunsAsync(
            project: project,
            buildUri: buildUri,
            cancellationToken: ct);

        return runs.Select(r => new TestRunInfo(
            Id: r.Id,
            Name: r.Name,
            BuildUri: r.BuildConfiguration?.BuildDefinitionId.ToString(),
            State: r.State,
            TotalTests: r.TotalTests,
            PassedTests: r.PassedTests,
            UnresolvedTests: r.IncompleteTests,
            StartedDate: r.StartedDate,
            CompletedDate: r.CompletedDate
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestResultInfo>> GetTestResultsAsync(
        string collectionUrl, string project, int testRunId,
        string? outcomeFilter = null,
        CancellationToken ct = default)
    {
        using var connection = CreateConnection(collectionUrl);
        var client = connection.GetClient<TestManagementHttpClient>();

        var outcomes = outcomeFilter is not null
            ? new List<TestOutcome> { Enum.Parse<TestOutcome>(outcomeFilter, ignoreCase: true) }
            : null;

        var results = await client.GetTestResultsAsync(
            project: project,
            runId: testRunId,
            outcomes: outcomes,
            cancellationToken: ct);

        return results.Select(r => new TestResultInfo(
            Id: r.Id,
            TestCaseTitle: r.TestCaseTitle,
            AutomatedTestName: r.AutomatedTestName,
            ComputerName: r.ComputerName,
            Outcome: r.Outcome,
            ErrorMessage: r.ErrorMessage,
            StackTrace: r.StackTrace,
            StartedDate: r.StartedDate,
            CompletedDate: r.CompletedDate,
            TestRunId: testRunId,
            TestRunName: null // populated by caller if needed
        )).ToList();
    }

    private VssConnection CreateConnection(string collectionUrl)
    {
        var credentials = new VssBasicCredential(string.Empty, _pat);
        return new VssConnection(new Uri(collectionUrl), credentials);
    }
}
