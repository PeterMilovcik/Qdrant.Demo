namespace FailedTests.Api.Models;

// -------------------------------------------------------
// Domain records for Azure DevOps integration.
// SDK-free types — the service layer maps SDK objects to these.
// -------------------------------------------------------

/// <summary>Subset of TestRun properties we care about.</summary>
public record TestRunInfo(
    int Id,
    string Name,
    string? BuildUri,
    string? State,
    int TotalTests,
    int PassedTests,
    int UnresolvedTests,
    DateTime? StartedDate,
    DateTime? CompletedDate
);

/// <summary>Subset of TestCaseResult properties returned by the AzDO API.</summary>
public record TestResultInfo(
    int Id,
    string? TestCaseTitle,
    string? AutomatedTestName,
    string? ComputerName,
    string? Outcome,
    string? ErrorMessage,
    string? StackTrace,
    DateTime? StartedDate,
    DateTime? CompletedDate,
    int TestRunId,
    string? TestRunName
);

/// <summary>Request body for <c>POST /index/build</c>.</summary>
public record IndexBuildRequest(
    string CollectionUrl,
    string ProjectName,
    int BuildId,
    string? DefinitionName = null
);

/// <summary>Response body for <c>POST /index/build</c>.</summary>
public record IndexBuildResponse(
    int BuildId,
    int TestRunsFound,
    int FailedResultsFound,
    int PointsIndexed,
    IReadOnlyList<string> Errors
);
