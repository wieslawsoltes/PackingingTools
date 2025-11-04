using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Sdk;

/// <summary>
/// Options for configuring the PackagingTools SDK client.
/// </summary>
public sealed class PackagingClientOptions
{
    /// <summary>
    /// Whether to register Windows packaging services. Default is <c>true</c>.
    /// </summary>
    public bool IncludeWindows { get; set; } = true;

    /// <summary>
    /// Whether to register macOS packaging services. Default is <c>true</c>.
    /// </summary>
    public bool IncludeMac { get; set; } = true;

    /// <summary>
    /// Whether to register Linux packaging services. Default is <c>true</c>.
    /// </summary>
    public bool IncludeLinux { get; set; } = true;

    /// <summary>
    /// Optional telemetry channel override. When null, a no-op telemetry channel is used.
    /// </summary>
    public ITelemetryChannel? TelemetryChannel { get; set; }

    /// <summary>
    /// Optional policy evaluator override. When null, <see cref="PackagingTools.Core.Policies.PolicyEngineEvaluator"/> is used.
    /// </summary>
    public IPolicyEvaluator? PolicyEvaluator { get; set; }

    /// <summary>
    /// Optional agent broker override. When null, a local in-process broker is used.
    /// </summary>
    public IBuildAgentBroker? AgentBroker { get; set; }

    /// <summary>
    /// Additional directories probed for plugins in addition to project metadata and defaults.
    /// </summary>
    public IList<string> PluginDirectories { get; } = new List<string>();

    /// <summary>
    /// Optional hook for adding or replacing services on the underlying <see cref="IServiceCollection"/>.
    /// </summary>
    public Action<IServiceCollection>? ConfigureServices { get; set; }
}
