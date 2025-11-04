using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Linux.Tooling;

namespace PackagingTools.Core.Linux.Signing;

public sealed class LinuxSigningService : ISigningService
{
    private readonly ILinuxProcessRunner _processRunner;

    public LinuxSigningService(ILinuxProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Properties is null || !request.Properties.TryGetValue("linux.signing.keyId", out var keyId))
        {
            return SigningResult.Succeeded();
        }

        var outputPath = request.Artifact.Path + ".sig";
        var args = new List<string>
        {
            "--batch",
            "--yes",
            "--local-user",
            keyId,
            "--output",
            outputPath,
            "--detach-sign",
            request.Artifact.Path
        };

        var result = await _processRunner.ExecuteAsync(new LinuxProcessRequest("gpg", args), cancellationToken);
        if (!result.IsSuccess)
        {
            return SigningResult.Failed(new PackagingIssue(
                "linux.signing.failed",
                $"gpg failed with {result.ExitCode}: {result.StandardError}",
                PackagingIssueSeverity.Error));
        }

        return SigningResult.Succeeded();
    }
}
