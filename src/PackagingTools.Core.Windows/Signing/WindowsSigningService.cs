using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Windows.Tooling;
using PackagingTools.Core.Windows.Signing.Azure;

namespace PackagingTools.Core.Windows.Signing;

/// <summary>
/// Signs Windows binaries using either local PFX files or remote providers.
/// </summary>
public sealed class WindowsSigningService : ISigningService
{
    private readonly IProcessRunner _processRunner;
    private readonly IAzureKeyVaultSigner _keyVaultSigner;

    public WindowsSigningService(IProcessRunner processRunner, IAzureKeyVaultSigner keyVaultSigner)
    {
        _processRunner = processRunner;
        _keyVaultSigner = keyVaultSigner;
    }

    public async Task<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Format is not ("msix" or "msi" or "exe"))
        {
            return SigningResult.Succeeded();
        }

        if (request.Properties is null || request.Properties.Count == 0)
        {
            return SigningResult.Succeeded();
        }

        if (request.Properties.TryGetValue("windows.signing.azureKeyVaultCertificate", out var remoteCertificate) &&
            !string.IsNullOrWhiteSpace(remoteCertificate))
        {
            return await _keyVaultSigner.SignAsync(request, remoteCertificate, cancellationToken).ConfigureAwait(false);
        }

        if (!request.Properties.TryGetValue("windows.signing.certificatePath", out var certificatePath) ||
            string.IsNullOrWhiteSpace(certificatePath))
        {
            return SigningResult.Succeeded();
        }

        request.Properties.TryGetValue("windows.signing.password", out var password);
        request.Properties.TryGetValue("windows.signing.timestampUrl", out var timestampUrl);
        var arguments = new List<string>
        {
            "sign",
            "/fd", request.Properties.TryGetValue("windows.signing.digestAlgorithm", out var digest) ? digest : "SHA256",
            "/f", certificatePath
        };

        if (!string.IsNullOrEmpty(password))
        {
            arguments.Add("/p");
            arguments.Add(password);
        }

        if (!string.IsNullOrEmpty(timestampUrl))
        {
            arguments.Add("/tr");
            arguments.Add(timestampUrl);
        }

        arguments.Add(request.Artifact.Path);

        var result = await _processRunner.ExecuteAsync(new ProcessExecutionRequest(
            "signtool.exe",
            string.Join(" ", arguments)), cancellationToken);

        if (!result.IsSuccess)
        {
            return SigningResult.Failed(new PackagingIssue(
                "windows.signing.failed",
                $"signtool failed ({result.ExitCode}): {result.StandardError}",
                PackagingIssueSeverity.Error));
        }

        return SigningResult.Succeeded();
    }
}
