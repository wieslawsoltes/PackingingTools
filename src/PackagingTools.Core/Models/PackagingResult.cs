namespace PackagingTools.Core.Models;

/// <summary>
/// Result of a packaging operation.
/// </summary>
/// <param name="Success">Indicates whether the pipeline completed without blocking errors.</param>
/// <param name="Artifacts">Collection of produced artifacts.</param>
/// <param name="Issues">Logged issues during the run.</param>
public sealed record PackagingResult(
    bool Success,
    IReadOnlyCollection<PackagingArtifact> Artifacts,
    IReadOnlyCollection<PackagingIssue> Issues)
{
    public static PackagingResult Failed(IReadOnlyCollection<PackagingIssue> issues)
        => new(false, Array.Empty<PackagingArtifact>(), issues);

    public static PackagingResult Succeeded(IReadOnlyCollection<PackagingArtifact> artifacts, IReadOnlyCollection<PackagingIssue>? issues = null)
        => new(true, artifacts, issues ?? Array.Empty<PackagingIssue>());
}
