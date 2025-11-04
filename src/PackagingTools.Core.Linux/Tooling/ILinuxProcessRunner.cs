using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Linux.Tooling;

/// <summary>
/// Abstraction for executing Linux packaging toolchain commands (dpkg, rpm, flatpak, snapcraft).
/// </summary>
public interface ILinuxProcessRunner
{
    Task<LinuxProcessResult> ExecuteAsync(LinuxProcessRequest request, CancellationToken cancellationToken = default);
}

public sealed record LinuxProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);

public sealed record LinuxProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
}
