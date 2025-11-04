using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Linux.Tooling;

public sealed class LinuxProcessRunner : ILinuxProcessRunner
{
    public async Task<LinuxProcessResult> ExecuteAsync(LinuxProcessRequest request, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in request.Arguments)
        {
            psi.ArgumentList.Add(argument);
        }

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

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdOut.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stdErr.AppendLine(e.Data); };
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
                         // ignore
                     }
                     tcs.TrySetCanceled(cancellationToken);
                 }))
        {
            var exitCode = await tcs.Task.ConfigureAwait(false);
            return new LinuxProcessResult(exitCode, stdOut.ToString(), stdErr.ToString());
        }
    }
}
