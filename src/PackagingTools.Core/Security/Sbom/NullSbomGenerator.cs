using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Security.Sbom;

/// <summary>
/// Default SBOM generator placeholder when no provider is configured.
/// </summary>
public sealed class NullSbomGenerator : ISbomGenerator
{
    public static NullSbomGenerator Instance { get; } = new();

    public string Format => "none";

    public Task<SbomGenerationResult> GenerateAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default)
        => Task.FromResult(new SbomGenerationResult(string.Empty, null));
}
