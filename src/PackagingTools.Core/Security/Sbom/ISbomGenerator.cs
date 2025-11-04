using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Security.Sbom;

/// <summary>
/// Generates a software bill of materials for packaging artifacts.
/// </summary>
public interface ISbomGenerator
{
    string Format { get; }

    Task<SbomGenerationResult> GenerateAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default);
}

public sealed record SbomGenerationResult(string Path, PackagingIssue? Issue);
