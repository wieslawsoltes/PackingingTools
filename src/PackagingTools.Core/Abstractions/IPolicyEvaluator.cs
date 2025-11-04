using PackagingTools.Core.Models;
using PackagingTools.Core.Security.Identity;

namespace PackagingTools.Core.Abstractions;

/// <summary>
/// Validates organizational policies before and after packaging runs.
/// </summary>
public interface IPolicyEvaluator
{
    Task<PolicyEvaluationResult> EvaluateAsync(PolicyEvaluationContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Captures the context required for policy evaluation.
/// </summary>
/// <param name="Project">Project definition.</param>
/// <param name="Request">Packaging request.</param>
/// <param name="Identity">Identity associated with the packaging run.</param>
public sealed record PolicyEvaluationContext(
    PackagingProject Project,
    PackagingRequest Request,
    IdentityResult? Identity);

/// <summary>
/// Result of a policy evaluation.
/// </summary>
/// <param name="IsAllowed">Whether execution may proceed.</param>
/// <param name="Issues">Policy issues that must be surfaced to the user.</param>
public sealed record PolicyEvaluationResult(bool IsAllowed, IReadOnlyCollection<PackagingIssue> Issues)
{
    public static PolicyEvaluationResult Allowed()
        => new(true, Array.Empty<PackagingIssue>());

    public static PolicyEvaluationResult Blocked(params PackagingIssue[] issues)
        => new(false, issues);
}
