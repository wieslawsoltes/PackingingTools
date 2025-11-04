using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Mac.Audit;

/// <summary>
/// Executes configured audit services after packaging completes.
/// </summary>
public sealed class AuditIntegrationService
{
    private readonly IEnumerable<IMacAuditService> _auditServices;

    public AuditIntegrationService(IEnumerable<IMacAuditService> auditServices)
    {
        _auditServices = auditServices;
    }

    public async Task<IReadOnlyCollection<PackagingIssue>> CaptureAsync(PackageFormatContext context, PackagingResult result, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        foreach (var service in _auditServices)
        {
            var captured = await service.CaptureAsync(context, result, cancellationToken).ConfigureAwait(false);
            issues.AddRange(captured);
        }

        return issues;
    }
}
