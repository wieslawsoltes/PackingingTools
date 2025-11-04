using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Windows.Tooling;

/// <summary>
/// Executes Windows tooling locally using System.Diagnostics.Process.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessExecutionResult> ExecuteAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = request.FileName,
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (request.Environment is not null)
        {
            foreach (var kvp in request.Environment)
            {
                psi.Environment[kvp.Key] = kvp.Value;
            }
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdOut.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdErr.AppendLine(e.Data);
            }
        };
        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{request.FileName}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (cancellationToken.Register(() =>
                 {
                     try
                     {
                         if (!process.HasExited)
                         {
                             process.Kill(entireProcessTree: true);
                         }
                     }
                     catch
                     {
                         // ignored
                     }
                     tcs.TrySetCanceled(cancellationToken);
                 }))
        {
            var exitCode = await tcs.Task.ConfigureAwait(false);
            return new ProcessExecutionResult(exitCode, stdOut.ToString(), stdErr.ToString());
        }
    }
}
