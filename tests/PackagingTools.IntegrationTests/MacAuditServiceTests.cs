using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Mac.Audit;
using PackagingTools.Core.Models;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class MacAuditServiceTests : System.IDisposable
{
    private readonly string _tempRoot;

    public MacAuditServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "MacAuditTests", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task CapturesNotarizationLog()
    {
        var project = new PackagingProject(
            "audit",
            "AuditApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>());

        var outputDir = Path.Combine(_tempRoot, "output");
        Directory.CreateDirectory(outputDir);
        var workingDir = Path.Combine(_tempRoot, "working");
        Directory.CreateDirectory(workingDir);

        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.MacOS,
            new[] { "app" },
            "Release",
            outputDir,
            new Dictionary<string, string>());

        var context = new PackageFormatContext(project, request, workingDir);

        var logPath = Path.Combine(_tempRoot, "notary.json");
        File.WriteAllText(logPath, "{}");

        var artifact = new PackagingArtifact(
            "app",
            Path.Combine(_tempRoot, "artifact.app"),
            new Dictionary<string, string>
            {
                ["notarizationLog"] = logPath,
                ["stapled"] = "True"
            });

        var result = new PackagingResult(true, new[] { artifact }, new PackagingIssue[0]);

        var service = new MacAuditService(NullLogger<MacAuditService>.Instance);
        var issues = await service.CaptureAsync(context, result);

        Assert.Empty(issues);
        var auditDir = Path.Combine(outputDir, "_Audit");
        Assert.True(Directory.Exists(auditDir));
        Assert.Contains(Directory.EnumerateFiles(auditDir, "*", SearchOption.AllDirectories), f => Path.GetFileName(f) == "notary.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
        catch
        {
        }
    }
}
