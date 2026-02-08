namespace FailedTests.Api.Models;

// ----------------------------
// DTOs (Azure DevOps aligned)
// ----------------------------

/// <summary>
/// Shape aligned to Azure DevOps SDK TestCaseResult (subset we care about).
/// See: https://learn.microsoft.com/en-us/dotnet/api/microsoft.teamfoundation.testmanagement.webapi.testcaseresult
/// </summary>
public record AzureDevOpsTestCaseResultDto(
    int Id,
    string? TestCaseTitle,
    string? AutomatedTestName,
    string? ComputerName,
    string? Outcome,
    string? ErrorMessage,
    string? StackTrace,
    DateTime? StartedDate,
    DateTime? CompletedDate
);

/// <summary>Envelope adds pipeline / build / test-run context around the test result.</summary>
public record FailedTestEnvelope(
    string ProjectName,
    string DefinitionName,
    int BuildId,
    string BuildName,
    int TestRunId,
    AzureDevOpsTestCaseResultDto Result
);

public record IndexResponse(
    string PointId,
    string SignatureId
);

public record SimilaritySearchRequest(
    string QueryText,
    float ScoreThreshold = 0.42f,  // return only results above this cosine-similarity score
    int Limit = 100,               // safety cap (not the primary control — threshold is)
    string? ProjectName = null,
    string? DefinitionName = null,
    long? FromTimestampMs = null,
    long? ToTimestampMs = null
);

public record MetadataSearchRequest(
    int Limit = 25,
    string? ProjectName = null,
    string? DefinitionName = null,
    string? TestName = null,
    string? Outcome = null,
    long? FromTimestampMs = null,
    long? ToTimestampMs = null
);
