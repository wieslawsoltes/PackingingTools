namespace PackagingTools.Core.Models;

/// <summary>
/// Represents a packaged artifact produced by the pipeline.
/// </summary>
/// <param name="Format">The packaging format (e.g. msix, msi).</param>
/// <param name="Path">Filesystem path to the artifact.</param>
/// <param name="Metadata">Additional metadata (hashes, version, manifest URIs).</param>
public sealed record PackagingArtifact(
    string Format,
    string Path,
    IReadOnlyDictionary<string, string> Metadata);
