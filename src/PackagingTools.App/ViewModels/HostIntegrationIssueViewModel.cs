using PackagingTools.Core.Windows.Configuration;

namespace PackagingTools.App.ViewModels;

public sealed class HostIntegrationIssueViewModel
{
    public HostIntegrationIssueViewModel(HostIntegrationIssue issue)
    {
        Issue = issue;
    }

    public HostIntegrationIssue Issue { get; }

    public string Severity => Issue.Severity == HostIntegrationIssueSeverity.Error ? "Error" : "Warning";

    public bool IsError => Issue.Severity == HostIntegrationIssueSeverity.Error;

    public string Message => Issue.Message;

    public string Display => $"[{Severity}] {Issue.Code}: {Issue.Message}";
}
