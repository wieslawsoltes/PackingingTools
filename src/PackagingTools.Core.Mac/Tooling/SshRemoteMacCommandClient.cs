using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Core.Mac.Tooling;

/// <summary>
/// Executes macOS tooling on a remote host via the OpenSSH client.
/// Requires the build agent to expose SSH connection metadata through capabilities.
/// </summary>
public sealed class SshRemoteMacCommandClient : IRemoteMacCommandClient
{
    private readonly ILogger<SshRemoteMacCommandClient>? _logger;

    public SshRemoteMacCommandClient(ILogger<SshRemoteMacCommandClient>? logger = null)
    {
        _logger = logger;
    }

    public bool CanExecute(IBuildAgentHandle agent)
        => agent.Capabilities.TryGetValue("mac.remote.sshHost", out var host) && !string.IsNullOrWhiteSpace(host);

    public async Task<MacProcessResult> ExecuteAsync(IBuildAgentHandle agent, MacProcessRequest request, CancellationToken cancellationToken = default)
    {
        if (!agent.Capabilities.TryGetValue("mac.remote.sshHost", out var host) || string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Remote agent is missing 'mac.remote.sshHost' capability.");
        }

        agent.Capabilities.TryGetValue("mac.remote.sshUser", out var user);
        var endpoint = string.IsNullOrWhiteSpace(user) ? host : $"{user}@{host}";

        var psi = new ProcessStartInfo
        {
            FileName = "ssh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (agent.Capabilities.TryGetValue("mac.remote.sshIdentity", out var identity) && !string.IsNullOrWhiteSpace(identity))
        {
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(identity);
        }

        if (agent.Capabilities.TryGetValue("mac.remote.sshPort", out var port) && !string.IsNullOrWhiteSpace(port))
        {
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add(port);
        }

        psi.ArgumentList.Add(endpoint);
        psi.ArgumentList.Add(BuildRemoteCommand(request));

        try
        {
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
                throw new InvalidOperationException("Failed to start ssh process.");
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
                return new MacProcessResult(exitCode, stdOut.ToString(), stdErr.ToString());
            }
        }
        catch (Win32Exception ex)
        {
            _logger?.LogError(ex, "SSH invocation failed when targeting agent {Agent}", agent.Name);
            return new MacProcessResult(-1, string.Empty, ex.Message);
        }
    }

    private static string BuildRemoteCommand(MacProcessRequest request)
    {
        var builder = new StringBuilder();

        if (request.Environment is not null)
        {
            foreach (var kvp in request.Environment)
            {
                builder.Append(kvp.Key);
                builder.Append('=');
                builder.Append(EscapeShellArgument(kvp.Value));
                builder.Append(' ');
            }
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            builder.Append("cd ");
            builder.Append(EscapeShellArgument(request.WorkingDirectory!));
            builder.Append(" && ");
        }

        builder.Append(EscapeShellArgument(request.FileName));
        foreach (var arg in request.Arguments)
        {
            builder.Append(' ');
            builder.Append(EscapeShellArgument(arg));
        }

        return builder.ToString();
    }

    private static string EscapeShellArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "''";
        }

        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }
}
