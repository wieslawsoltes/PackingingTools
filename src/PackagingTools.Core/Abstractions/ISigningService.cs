using PackagingTools.Core.Models;

namespace PackagingTools.Core.Abstractions;

/// <summary>
/// Provides signing operations for packaging artifacts.
/// </summary>
public interface ISigningService
{
    Task<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a signing request.
/// </summary>
/// <param name="Artifact">Artifact to sign.</param>
/// <param name="Format">Format-specific hint (e.g. msix).</param>
/// <param name="Properties">Signing properties (e.g. certificate thumbprint).</param>
public sealed record SigningRequest(
    PackagingArtifact Artifact,
    string Format,
    IReadOnlyDictionary<string, string>? Properties = null);

/// <summary>
/// Result of signing.
/// </summary>
/// <param name="Success">Indicates success.</param>
/// <param name="Issues">Issues raised.</param>
public sealed record SigningResult(bool Success, IReadOnlyCollection<PackagingIssue> Issues)
{
    public static SigningResult Succeeded()
        => new(true, Array.Empty<PackagingIssue>());

    public static SigningResult Failed(params PackagingIssue[] issues)
        => new(false, issues);
}
