namespace PackagingTools.Core.Windows.Configuration;

/// <summary>
/// Represents a validation message for Windows host integration metadata.
/// </summary>
/// <param name="Code">Stable identifier for the issue.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Severity">Severity of the issue.</param>
public sealed record HostIntegrationIssue(
    string Code,
    string Message,
    HostIntegrationIssueSeverity Severity);

public enum HostIntegrationIssueSeverity
{
    Warning,
    Error
}
