using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Mac.Tooling;

namespace PackagingTools.Core.Mac.Signing;

public sealed class MacSigningService : ISigningService
{
    private readonly IMacProcessRunner _processRunner;

    public MacSigningService(IMacProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Properties is null || !request.Properties.TryGetValue("mac.signing.identity", out var identity) || string.IsNullOrWhiteSpace(identity))
        {
            return SigningResult.Succeeded();
        }

        var args = new List<string>
        {
            "--force",
            "--deep",
            "--options",
            "runtime",
            "--sign",
            identity
        };

        if (request.Properties.TryGetValue("mac.signing.entitlements", out var entitlements))
        {
            args.Add("--entitlements");
            args.Add(entitlements);
        }

        args.Add(request.Artifact.Path);

        var result = await _processRunner.ExecuteAsync(new MacProcessRequest("codesign", args), cancellationToken);
        if (!result.IsSuccess)
        {
            return SigningResult.Failed(new PackagingIssue(
                "mac.signing.failed",
                $"codesign failed with {result.ExitCode}: {result.StandardError}",
                PackagingIssueSeverity.Error));
        }

        return SigningResult.Succeeded();
    }
}
