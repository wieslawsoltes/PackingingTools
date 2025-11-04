using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Utilities;

namespace PackagingTools.Core.AppServices;

public sealed record EnvironmentValidationRequest(
    bool RequireWindows,
    bool RequireMac,
    bool RequireLinux);

public sealed record EnvironmentCheckResult(
    string Id,
    string Title,
    bool Passed,
    string? Details = null,
    string? Remediation = null);

/// <summary>
/// Runs host environment readiness checks used by the GUI wizard and automation onboarding.
/// </summary>
public sealed class EnvironmentValidationService
{
    private readonly ToolLocator _toolLocator;

    private static readonly ToolCheckDescriptor[] CommonToolChecks =
    {
        new("env.tool.dotnet", ".NET SDK (dotnet)", "dotnet", "Install the .NET 8 SDK or newer from https://dotnet.microsoft.com/download."),
        new("env.tool.git", "Git CLI (git)", "git", "Install Git and ensure it is available on PATH.")
    };

    private static readonly ToolCheckDescriptor[] WindowsToolChecks =
    {
        new("env.tool.wix.candle", "WiX candle", "candle", "Install WiX v4 and add its tools directory to PATH."),
        new("env.tool.wix.light", "WiX light", "light", "Install WiX v4 and add its tools directory to PATH."),
        new("env.tool.signtool", "SignTool", "signtool", "Install Windows SDK signing tools or configure remote signing."),
        new("env.tool.makeappx", "MakeAppx", "makeappx", "Install Windows SDK or MSIX Packaging Tools.")
    };

    private static readonly ToolCheckDescriptor[] MacToolChecks =
    {
        new("env.tool.codesign", "codesign", "codesign", "Install Xcode Command Line Tools (xcode-select --install)."),
        new("env.tool.notarytool", "notarytool", "notarytool", "Install Xcode Command Line Tools or Xcode 14+."),
        new("env.tool.productbuild", "productbuild", "productbuild", "Install Xcode Command Line Tools or Xcode 14+."),
        new("env.tool.stapler", "stapler", "stapler", "Install Xcode Command Line Tools or Xcode 14+.")
    };

    private static readonly ToolCheckDescriptor[] LinuxToolChecks =
    {
        new("env.tool.appimagetool", "AppImageTool", "appimagetool", "Install AppImageKit (https://github.com/AppImage/AppImageKit)."),
        new("env.tool.flatpak-builder", "flatpak-builder", "flatpak-builder", "Install Flatpak tooling via your distribution package manager."),
        new("env.tool.rpmbuild", "rpmbuild", "rpmbuild", "Install rpm-build tooling for building RPM packages."),
        new("env.tool.dpkg-deb", "dpkg-deb", "dpkg-deb", "Install dpkg-dev package to enable Deb packaging.")
    };

    public EnvironmentValidationService()
        : this(new ToolLocator())
    {
    }

    public EnvironmentValidationService(ToolLocator toolLocator)
    {
        _toolLocator = toolLocator;
    }

    public Task<IReadOnlyList<EnvironmentCheckResult>> ValidateAsync(EnvironmentValidationRequest request, CancellationToken cancellationToken = default)
    {
        var results = new List<EnvironmentCheckResult>();

        foreach (var osResult in EvaluateOperatingSystemRequirements(request))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(osResult);
        }

        foreach (var descriptor in EnumerateToolChecks(request))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(EvaluateTool(descriptor));
        }

        return Task.FromResult<IReadOnlyList<EnvironmentCheckResult>>(results);
    }

    private static IEnumerable<EnvironmentCheckResult> EvaluateOperatingSystemRequirements(EnvironmentValidationRequest request)
    {
        if (request.RequireWindows)
        {
            var passed = OperatingSystem.IsWindows();
            yield return new EnvironmentCheckResult(
                "env.os.windows",
                "Windows host available",
                passed,
                passed ? "Running on Windows host." : "Current host is not Windows.",
                passed ? null : "Run PackagingTools on Windows 10+/Server 2022 or connect to a Windows build agent.");
        }

        if (request.RequireMac)
        {
            var passed = OperatingSystem.IsMacOS();
            yield return new EnvironmentCheckResult(
                "env.os.mac",
                "macOS host available",
                passed,
                passed ? "Running on macOS host." : "Current host is not macOS.",
                passed ? null : "Run PackagingTools on macOS 13+ or configure a remote macOS agent.");
        }

        if (request.RequireLinux)
        {
            var passed = OperatingSystem.IsLinux();
            yield return new EnvironmentCheckResult(
                "env.os.linux",
                "Linux host available",
                passed,
                passed ? "Running on Linux host." : "Current host is not Linux.",
                passed ? null : "Run PackagingTools on Linux or connect to a Linux build agent.");
        }
    }

    private static IEnumerable<ToolCheckDescriptor> EnumerateToolChecks(EnvironmentValidationRequest request)
    {
        var set = new Dictionary<string, ToolCheckDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in CommonToolChecks)
        {
            set.TryAdd(descriptor.Id, descriptor);
        }

        if (request.RequireWindows)
        {
            foreach (var descriptor in WindowsToolChecks)
            {
                set.TryAdd(descriptor.Id, descriptor);
            }
        }

        if (request.RequireMac)
        {
            foreach (var descriptor in MacToolChecks)
            {
                set.TryAdd(descriptor.Id, descriptor);
            }
        }

        if (request.RequireLinux)
        {
            foreach (var descriptor in LinuxToolChecks)
            {
                set.TryAdd(descriptor.Id, descriptor);
            }
        }

        return set.Values.OrderBy(d => d.Title, StringComparer.Ordinal);
    }

    private EnvironmentCheckResult EvaluateTool(ToolCheckDescriptor descriptor)
    {
        if (_toolLocator.TryLocate(descriptor.ToolName, out var path))
        {
            return new EnvironmentCheckResult(descriptor.Id, descriptor.Title, true, $"Found at {path}");
        }

        return new EnvironmentCheckResult(descriptor.Id, descriptor.Title, false, "Not found on PATH.", descriptor.Remediation);
    }

    private sealed record ToolCheckDescriptor(string Id, string Title, string ToolName, string Remediation);
}
