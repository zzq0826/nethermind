using Nethermind.Monitoring.Metrics;
using Prometheus;

namespace Nethermind.Monitoring.Model;

public readonly struct Histogram
{
    internal static bool Enabled = false;

    private readonly Prometheus.Histogram _inner;

    public static Histogram NewLatencyHistogram(string propertyName, params string[] labels)
    {
        return new Histogram(
            Prometheus.Metrics.CreateHistogram(MetricsUpdater.BuildMetricNameFromPropertyName(propertyName),
                "",
                new HistogramConfiguration()
             {
                 Buckets = Prometheus.Histogram.PowersOfTenDividedBuckets(1, 4, 4),
                 LabelNames = labels,
             }));
    }

    private Histogram(Prometheus.Histogram inner)
    {
        _inner = inner;
    }

    public Child WithLabels(params string[] labels)
    {
        return new Child(_inner.WithLabels(labels));
    }

    public void Observe(double observation)
    {
        if (Enabled) _inner.Observe(observation);
    }

    public readonly struct Child
    {
        private readonly Prometheus.Histogram.Child _inner;

        internal Child(Prometheus.Histogram.Child inner)
        {
            _inner = inner;
        }

        public void Observe(double observation)
        {
            if (Enabled) _inner.Observe(observation);
        }
    }
}
