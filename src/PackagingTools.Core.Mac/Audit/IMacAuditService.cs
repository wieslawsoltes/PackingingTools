using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Models;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Core.Mac.Audit;

/// <summary>
/// Captures audit artifacts (logs, receipts) for macOS packaging workflows.
/// </summary>
public interface IMacAuditService
{
    Task<IReadOnlyCollection<PackagingIssue>> CaptureAsync(PackageFormatContext context, PackagingResult result, CancellationToken cancellationToken = default);
}
