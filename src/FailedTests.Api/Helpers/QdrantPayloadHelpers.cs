using FailedTests.Api.Models;

namespace FailedTests.Api.Helpers;

/// <summary>
/// Helpers for converting between Qdrant gRPC payload values and plain CLR objects.
/// </summary>
/// <remarks>
/// ⚠️  Qdrant.Client uses <see cref="Qdrant.Client.Grpc.Value"/> —
/// NOT <c>Google.Protobuf.WellKnownTypes.Value</c>.
/// Use <c>DoubleValue</c> / <c>IntegerValue</c> (there is no <c>NumberValue</c>).
/// </remarks>
public static class QdrantPayloadHelpers
{
    /// <summary>Convert a Qdrant payload dictionary to plain CLR objects for clean JSON output.</summary>
    public static Dictionary<string, object?> PayloadToDictionary(
        IDictionary<string, Qdrant.Client.Grpc.Value> payload)
    {
        return payload.ToDictionary(kv => kv.Key, kv => FromProto(kv.Value));
    }

    /// <summary>
    /// Build the Qdrant REST scroll-filter object from a <see cref="MetadataSearchRequest"/>.
    /// Returns <c>null</c> when no filters are specified.
    /// </summary>
    public static object? BuildScrollFilter(MetadataSearchRequest req)
    {
        var must = new List<object>();

        void AddMatch(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            must.Add(new { key, match = new { value } });
        }

        AddMatch("project_name", req.ProjectName);
        AddMatch("definition_name", req.DefinitionName);
        AddMatch("test_name", req.TestName);
        AddMatch("outcome", req.Outcome);

        if (req.FromTimestampMs is not null || req.ToTimestampMs is not null)
        {
            must.Add(new
            {
                key = "timestamp_ms",
                range = new
                {
                    gte = req.FromTimestampMs,
                    lte = req.ToTimestampMs
                }
            });
        }

        if (must.Count == 0) return null;

        return new { must };
    }

    // ---- private ----

    private static object? FromProto(Qdrant.Client.Grpc.Value v) =>
        v.KindCase switch
        {
            Qdrant.Client.Grpc.Value.KindOneofCase.StringValue  => v.StringValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.DoubleValue  => v.DoubleValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.IntegerValue => v.IntegerValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.BoolValue    => v.BoolValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.StructValue  =>
                v.StructValue.Fields.ToDictionary(f => f.Key, f => FromProto(f.Value)),
            Qdrant.Client.Grpc.Value.KindOneofCase.ListValue    =>
                v.ListValue.Values.Select(FromProto).ToList(),
            _ => null
        };
}
