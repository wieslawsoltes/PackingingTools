namespace PackagingTools.Core.Models;

/// <summary>
/// Severity level for issues raised during packaging.
/// </summary>
public enum PackagingIssueSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Represents an issue encountered while packaging.
/// </summary>
/// <param name="Code">Machine-readable error or warning code.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Severity">Issue severity.</param>
public sealed record PackagingIssue(
    string Code,
    string Message,
    PackagingIssueSeverity Severity);
