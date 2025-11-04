using System;
using System.IO;
using System.Text;

namespace PackagingTools.Core.Windows.Tooling;

/// <summary>
/// Persists process execution details to diagnostic log files for troubleshooting.
/// </summary>
public static class ProcessDiagnosticsWriter
{
    public static string? TryWriteProcessLog(
        string outputDirectory,
        string component,
        ProcessExecutionRequest request,
        ProcessExecutionResult result)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            var diagnosticsDirectory = Path.Combine(outputDirectory, "_diagnostics");
            Directory.CreateDirectory(diagnosticsDirectory);

            var fileName = $"{Sanitize(component)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.log";
            var fullPath = Path.Combine(diagnosticsDirectory, fileName);

            var builder = new StringBuilder();
            builder.AppendLine($"# Timestamp: {DateTimeOffset.UtcNow:O}");
            builder.AppendLine($"# Tool: {request.FileName}");
            builder.AppendLine($"# Arguments: {request.Arguments}");
            builder.AppendLine($"# ExitCode: {result.ExitCode}");
            builder.AppendLine();
            builder.AppendLine("## Standard Output");
            builder.AppendLine(result.StandardOutput);
            builder.AppendLine();
            builder.AppendLine("## Standard Error");
            builder.AppendLine(result.StandardError);

            File.WriteAllText(fullPath, builder.ToString(), Encoding.UTF8);
            return fullPath;
        }
        catch
        {
            return null;
        }
    }

    private static string Sanitize(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }
}
