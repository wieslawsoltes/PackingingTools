using System;
using System.Collections.Generic;
using System.Reflection;
using PackagingTools.App.ViewModels;
using PackagingTools.Core.AppServices;
using PackagingTools.Core.Audit;
using PackagingTools.Core.Models;

namespace PackagingTools.IntegrationTests;

public class ConfigurationAuditViewModelTests
{
    [Fact]
    public void RequestRollbackCommand_FiresEventForBaselineSnapshot()
    {
        var service = new ConfigurationAuditService();
        var viewModel = new ConfigurationAuditViewModel(service);

        var baseline = CreateProject("1.0.0", new Dictionary<string, string> { ["owner"] = "team-a" });
        viewModel.CaptureSnapshot(baseline, "tester", "baseline");

        Assert.False(viewModel.RequestRollbackCommand.CanExecute(null));

        var updated = CreateProject("1.1.0", new Dictionary<string, string> { ["owner"] = "team-b" });
        viewModel.CaptureSnapshot(updated, "tester", "updated");

        Assert.NotNull(viewModel.SelectedSnapshot);
        Assert.NotNull(viewModel.ComparisonSnapshot);
        Assert.True(viewModel.RequestRollbackCommand.CanExecute(null));

        Guid? requestedId = null;
        viewModel.RollbackRequested += (_, id) => requestedId = id;

        viewModel.RequestRollbackCommand.Execute(null);

        Assert.NotNull(requestedId);
        Assert.Equal(viewModel.ComparisonSnapshot!.Id, requestedId);
    }

    [Fact]
    public void MainWindowViewModel_AppliesRollbackSnapshot()
    {
        var vm = new MainWindowViewModel();
        var workspaceField = typeof(MainWindowViewModel).GetField("_workspace", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(workspaceField);
        var workspace = (ProjectWorkspace)workspaceField!.GetValue(vm)!;

        var baseline = CreateProject("1.0.0", new Dictionary<string, string> { ["owner"] = "team-a" });
        workspace.Initialize(baseline, "project.json");
        InvokePopulate(vm);
        vm.Audit.CaptureSnapshot(baseline, "tester", "baseline");

        var updated = CreateProject("1.1.0", new Dictionary<string, string> { ["owner"] = "team-b" });
        workspace.Initialize(updated, "project.json");
        InvokePopulate(vm);
        vm.Audit.CaptureSnapshot(updated, "tester", "updated");

        vm.Audit.RequestRollbackCommand.Execute(null);

        var currentProject = workspace.CurrentProject;
        Assert.NotNull(currentProject);
        Assert.Equal("team-a", currentProject!.Metadata["owner"]);
    }

    private static PackagingProject CreateProject(string version, IReadOnlyDictionary<string, string> metadata)
        => new("sample.app", "Sample", version, metadata, new Dictionary<PackagingPlatform, PlatformConfiguration>
        {
            [PackagingPlatform.Linux] = new(new[] { "deb" }, new Dictionary<string, string> { ["linux.packageRoot"] = "./root" })
        });
    private static void InvokePopulate(MainWindowViewModel vm)
    {
        var method = typeof(MainWindowViewModel).GetMethod("PopulateFromWorkspace", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(vm, null);
        method = typeof(MainWindowViewModel).GetMethod("RefreshHostIntegrationBaseline", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(vm, null);
    }
}
