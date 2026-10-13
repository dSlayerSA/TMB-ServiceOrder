using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace OrderManagement.Infrastructure.Observability;

/// <summary>
/// Activity source and W3C trace-context plumbing for the asynchronous hops.
///
/// HTTP and database spans are produced by the built-in instrumentation, but a
/// message crossing RabbitMQ carries no ambient context: it has to be written
/// into the AMQP headers by the publisher and read back by the consumer, or the
/// Worker's work shows up as a separate, unconnected trace.
/// </summary>
public static class Telemetry
{
    public const string ServiceNameApi = "order-management-api";
    public const string ServiceNameWorker = "order-management-worker";

    public static readonly ActivitySource Source = new("OrderManagement");

    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    /// <summary>Writes the current trace context into AMQP headers as "traceparent"/"tracestate".</summary>
    public static void InjectTraceContext(Activity? activity, IDictionary<string, object?> headers)
    {
        var source = activity ?? Activity.Current;
        var context = source is null
            ? new PropagationContext(default, Baggage.Current)
            : new PropagationContext(source.Context, Baggage.Current);

        Propagator.Inject(context, headers, (carrier, key, value) => carrier[key] = value);
    }

    /// <summary>Reads the trace context a publisher wrote into the AMQP headers.</summary>
    public static PropagationContext ExtractTraceContext(AmqpHeaderCarrier headers) =>
        Propagator.Extract(default, headers, static (carrier, key) => carrier.Get(key));

    /// <summary>
    /// Parses a traceparent captured earlier (e.g. persisted on an outbox row) so a
    /// span produced later can still be attached to the request that caused it.
    /// </summary>
    public static ActivityContext ParseTraceParent(string? traceParent) =>
        ActivityContext.TryParse(traceParent, traceState: null, out var context)
            ? context
            : default;

    /// <summary>
    /// Adapter over the AMQP header dictionary: RabbitMQ hands string values back
    /// as byte[], which the propagator does not understand on its own.
    /// </summary>
    public sealed class AmqpHeaderCarrier
    {
        private readonly IDictionary<string, object?>? _headers;

        public AmqpHeaderCarrier(IDictionary<string, object?>? headers) => _headers = headers;

        public IEnumerable<string> Get(string key)
        {
            if (_headers is null || !_headers.TryGetValue(key, out var value))
                return [];

            return value switch
            {
                byte[] bytes => [Encoding.UTF8.GetString(bytes)],
                string text => [text],
                _ => []
            };
        }
    }
}
