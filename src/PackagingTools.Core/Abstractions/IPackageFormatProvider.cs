using PackagingTools.Core.Models;

namespace PackagingTools.Core.Abstractions;

/// <summary>
/// Provides packaging logic for a specific installer format.
/// </summary>
public interface IPackageFormatProvider
{
    /// <summary>
    /// Gets the canonical format identifier (e.g. "msix", "msi").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Builds the package for the provided context.
    /// </summary>
    Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to format providers.
/// </summary>
/// <param name="Project">Project definition.</param>
/// <param name="Request">Parent packaging request.</param>
/// <param name="WorkingDirectory">Working directory reserved for temporary artifacts.</param>
public sealed record PackageFormatContext(
    PackagingProject Project,
    PackagingRequest Request,
    string WorkingDirectory);

/// <summary>
/// Result of executing a format provider.
/// </summary>
/// <param name="Artifacts">Artifacts produced by the provider.</param>
/// <param name="Issues">Issues raised while building.</param>
public sealed record PackageFormatResult(
    IReadOnlyCollection<PackagingArtifact> Artifacts,
    IReadOnlyCollection<PackagingIssue> Issues)
{
    public static PackageFormatResult Empty()
        => new(Array.Empty<PackagingArtifact>(), Array.Empty<PackagingIssue>());
}
