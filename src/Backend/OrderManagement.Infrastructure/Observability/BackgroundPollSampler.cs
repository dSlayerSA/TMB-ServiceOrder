using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OrderManagement.Infrastructure.Observability;

/// <summary>
/// Drops traces that are nothing but a background database round trip.
///
/// The outbox publisher polls Postgres every couple of seconds and Npgsql emits
/// a client span per query. With no request in flight those spans become their
/// own root traces, which quickly bury the order traces that actually matter.
/// A client span with no parent is always one of those polls; the same query
/// spans nested inside a real request inherit that request's decision and are
/// still recorded.
/// </summary>
internal sealed class BackgroundPollSampler : Sampler
{
    private readonly Sampler _inner = new ParentBasedSampler(new AlwaysOnSampler());

    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        var isRoot = parameters.ParentContext.TraceId == default;

        if (isRoot && parameters.Kind == ActivityKind.Client)
            return new SamplingResult(SamplingDecision.Drop);

        return _inner.ShouldSample(parameters);
    }
}
