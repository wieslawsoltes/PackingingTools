using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Configuration;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.AppServices;

/// <summary>
/// Provides high-level operations for loading and saving project workspaces used by CLI and GUI.
/// </summary>
public sealed class ProjectWorkspace
{
    public PackagingProject? CurrentProject { get; private set; }
    public string? ProjectPath { get; private set; }

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        CurrentProject = await PackagingProjectSerializer.LoadAsync(path, cancellationToken);
        ProjectPath = path;
    }

    public async Task SaveAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            throw new InvalidOperationException("No project loaded.");
        }
        var target = path ?? ProjectPath ?? throw new InvalidOperationException("Target path not specified.");
        await PackagingProjectSerializer.SaveAsync(CurrentProject, target, cancellationToken);
        ProjectPath = target;
    }

    public void UpdatePlatformConfiguration(PackagingPlatform platform, PlatformConfiguration configuration)
    {
        if (CurrentProject is null)
        {
            throw new InvalidOperationException("No project loaded.");
        }
        var platforms = new Dictionary<PackagingPlatform, PlatformConfiguration>(CurrentProject.Platforms)
        {
            [platform] = configuration
        };
        CurrentProject = CurrentProject with { Platforms = platforms };
    }

    public void Initialize(PackagingProject project, string? path = null)
    {
        CurrentProject = project ?? throw new ArgumentNullException(nameof(project));
        ProjectPath = path;
    }
}
