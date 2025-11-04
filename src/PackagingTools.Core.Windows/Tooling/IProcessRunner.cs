using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Windows.Tooling;

/// <summary>
/// Abstracts process execution so it can be mocked in tests and redirected to remote agents.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessExecutionResult> ExecuteAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data required to execute a process.
/// </summary>
/// <param name="FileName">Executable path.</param>
/// <param name="Arguments">Command-line arguments.</param>
/// <param name="WorkingDirectory">Optional working directory.</param>
/// <param name="Environment">Optional environment variables overrides.</param>
public sealed record ProcessExecutionRequest(
    string FileName,
    string Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);

/// <summary>
/// Result of executing a process.
/// </summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Captured stdout.</param>
/// <param name="StandardError">Captured stderr.</param>
public sealed record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
}
