using FailedTests.Api.Models;

namespace FailedTests.Api.Services;

/// <summary>
/// Shared indexing logic: embed a failed test result and upsert it into Qdrant.
/// Used by both <c>/index/test-result</c> and <c>/index/build</c> endpoints.
/// </summary>
public interface ITestResultIndexer
{
    /// <summary>
    /// Generate embedding for the given test failure, build a Qdrant point,
    /// and upsert it into the specified collection.
    /// </summary>
    Task<IndexResponse> IndexTestResultAsync(
        FailedTestEnvelope envelope,
        CancellationToken ct = default);
}
