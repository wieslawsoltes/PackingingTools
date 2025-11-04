using PackagingTools.Core.Models;

namespace PackagingTools.Core.Abstractions;

/// <summary>
/// Emits telemetry events and metrics for packaging runs.
/// </summary>
public interface ITelemetryChannel
{
    void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? properties = null);
    void TrackDependency(string dependencyName, TimeSpan duration, bool success, IReadOnlyDictionary<string, object?>? properties = null);
}
