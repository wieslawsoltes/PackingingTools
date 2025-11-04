using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Mac.Verification;

/// <summary>
/// Executes post-packaging verification runs for macOS artifacts.
/// </summary>
public interface IMacVerificationService
{
    Task<MacVerificationResult> VerifyAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result describing verification outcomes.
/// </summary>
/// <param name="Success">Whether verification succeeded.</param>
/// <param name="Issues">Issues raised during verification.</param>
public sealed record MacVerificationResult(bool Success, IReadOnlyCollection<PackagingIssue> Issues)
{
    public static MacVerificationResult Succeeded()
        => new(true, Array.Empty<PackagingIssue>());

    public static MacVerificationResult Failed(params PackagingIssue[] issues)
        => new(false, issues);
}
