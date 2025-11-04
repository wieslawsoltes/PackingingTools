using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Mac.Tooling;

/// <summary>
/// Abstraction for invoking macOS command-line tooling (codesign, productbuild, notarytool).
/// </summary>
public interface IMacProcessRunner
{
    Task<MacProcessResult> ExecuteAsync(MacProcessRequest request, CancellationToken cancellationToken = default);
}

public sealed record MacProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);

public sealed record MacProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
}
