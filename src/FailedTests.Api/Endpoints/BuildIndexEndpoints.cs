using Microsoft.AspNetCore.Mvc;
using FailedTests.Api.Models;
using FailedTests.Api.Services;

namespace FailedTests.Api.Endpoints;

public static class BuildIndexEndpoints
{
    /// <summary>
    /// Maps <c>POST /index/build</c> — pull failed test results from Azure DevOps
    /// for a given build and index them into Qdrant.
    /// </summary>
    public static WebApplication MapBuildIndexEndpoints(
        this WebApplication app, string collectionName)
    {
        app.MapPost("/index/build", async (
            [FromBody] IndexBuildRequest req,
            IAzureDevOpsService azdo,
            ITestResultIndexer indexer,
            CancellationToken ct) =>
        {
            try
            {
                // 1. Fetch test runs for the build
                var testRuns = await azdo.GetTestRunsForBuildAsync(
                    req.CollectionUrl, req.ProjectName, req.BuildId, ct);

                if (testRuns.Count == 0)
                {
                    return Results.Ok(new IndexBuildResponse(
                        BuildId: req.BuildId,
                        TestRunsFound: 0,
                        FailedResultsFound: 0,
                        PointsIndexed: 0,
                        Errors: Array.Empty<string>()));
                }

                var definitionName = req.DefinitionName ?? $"Build-{req.BuildId}";
                var totalFailed = 0;
                var totalIndexed = 0;
                var errors = new List<string>();

                // 2. For each test run, fetch failed results and index them
                foreach (var run in testRuns)
                {
                    IReadOnlyList<TestResultInfo> failedResults;
                    try
                    {
                        failedResults = await azdo.GetTestResultsAsync(
                            req.CollectionUrl, req.ProjectName, run.Id,
                            outcomeFilter: "Failed", ct: ct);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Run {run.Id} ({run.Name}): failed to fetch results — {ex.Message}");
                        continue;
                    }

                    totalFailed += failedResults.Count;

                    foreach (var result in failedResults)
                    {
                        try
                        {
                            var envelope = new FailedTestEnvelope(
                                ProjectName: req.ProjectName,
                                DefinitionName: definitionName,
                                BuildId: req.BuildId,
                                BuildName: run.Name ?? $"Run-{run.Id}",
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

                            await indexer.IndexTestResultAsync(envelope, ct);
                            totalIndexed++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add(
                                $"Run {run.Id}, result {result.Id}: indexing failed — {ex.Message}");
                        }
                    }
                }

                return Results.Ok(new IndexBuildResponse(
                    BuildId: req.BuildId,
                    TestRunsFound: testRuns.Count,
                    FailedResultsFound: totalFailed,
                    PointsIndexed: totalIndexed,
                    Errors: errors));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[index/build] Error: {ex.Message}");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Build indexing failed");
            }
        });

        return app;
    }
}
