using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Linux.Repos;
using PackagingTools.Core.Models;
using PackagingTools.Core.Utilities;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class LinuxRepositoryPublisherTests
{
    [Fact]
    public async Task PublishAsync_WritesAptMetadata()
    {
        await using var temp = TemporaryDirectoryScope.Create("apt-repo-test");
        var outputDir = temp.DirectoryPath;

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["linux.repo.enabled"] = "true",
            ["linux.repo.targets"] = "stable",
            ["linux.repo.target.stable.type"] = "apt",
            ["linux.repo.target.stable.suite"] = "stable",
            ["linux.repo.target.stable.components"] = "main",
            ["linux.repo.target.stable.destination"] = "s3://bucket/stable"
        };

        var request = new PackagingRequest("proj", PackagingPlatform.Linux, new[] { "deb" }, "Release", outputDir, properties);
        var project = new PackagingProject(
            "proj",
            "SampleLinux",
            "1.2.3",
            new Dictionary<string, string>
            {
                ["linux.architecture"] = "amd64"
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new[] { "deb" }, new Dictionary<string, string>())
            });

        var artifactPath = Path.Combine(outputDir, "sample.deb");
        await File.WriteAllTextAsync(artifactPath, "deb-content");

        var artifact = new PackagingArtifact(
            "deb",
            artifactPath,
            new Dictionary<string, string>
            {
                ["packageName"] = "sample-app",
                ["packageVersion"] = "1.2.3",
                ["packageArchitecture"] = "amd64",
                ["packageDescription"] = "Sample Application"
            });

        var publisher = new LinuxRepositoryPublisher(new StubCredentialProvider());
        var issues = await publisher.PublishAsync(new PackageFormatContext(project, request, outputDir), new PackagingResult(true, new[] { artifact }, Array.Empty<PackagingIssue>()));

        Assert.Empty(issues);

        var packagesPath = Path.Combine(outputDir, "_Repo", "stable", "apt", "dists", "stable", "main", "binary-amd64", "Packages");
        Assert.True(File.Exists(packagesPath));
        var packagesContent = await File.ReadAllTextAsync(packagesPath);
        Assert.Contains("Package: sample-app", packagesContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SHA256:", packagesContent, StringComparison.OrdinalIgnoreCase);

        var releasePath = Path.Combine(outputDir, "_Repo", "stable", "apt", "dists", "stable", "Release");
        Assert.True(File.Exists(releasePath));

        var targetMetadata = Path.Combine(outputDir, "_Repo", "stable", "apt", "target.json");
        Assert.True(File.Exists(targetMetadata));
    }

    [Fact]
    public async Task PublishAsync_WritesYumMetadata()
    {
        await using var temp = TemporaryDirectoryScope.Create("yum-repo-test");
        var outputDir = temp.DirectoryPath;

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["linux.repo.enabled"] = "true",
            ["linux.repo.targets"] = "prod",
            ["linux.repo.target.prod.type"] = "yum",
            ["linux.repo.target.prod.destination"] = "https://repo.example.com/yum",
            ["linux.repo.target.prod.credential"] = "s3"
        };

        var request = new PackagingRequest("proj", PackagingPlatform.Linux, new[] { "rpm" }, "Release", outputDir, properties);
        var project = new PackagingProject(
            "proj",
            "SampleLinux",
            "1.5.0",
            new Dictionary<string, string>
            {
                ["linux.architecture"] = "x86_64"
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new[] { "rpm" }, new Dictionary<string, string>())
            });

        var artifactPath = Path.Combine(outputDir, "sample.rpm");
        await File.WriteAllTextAsync(artifactPath, "rpm-content");

        var artifact = new PackagingArtifact(
            "rpm",
            artifactPath,
            new Dictionary<string, string>
            {
                ["packageName"] = "sample-app",
                ["packageVersion"] = "1.5.0",
                ["packageArchitecture"] = "x86_64"
            });

        var credential = new RepositoryCredential("s3", "token", new Dictionary<string, string>
        {
            ["env"] = "REPO_TOKEN"
        });
        var credentialProvider = new StubCredentialProvider();
        credentialProvider.Set("s3", credential);

        var publisher = new LinuxRepositoryPublisher(credentialProvider);
        var issues = await publisher.PublishAsync(new PackageFormatContext(project, request, outputDir), new PackagingResult(true, new[] { artifact }, Array.Empty<PackagingIssue>()));

        Assert.Empty(issues);

        var repodataPath = Path.Combine(outputDir, "_Repo", "prod", "yum", "repodata.json");
        Assert.True(File.Exists(repodataPath));

        var repoJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "_Repo", "prod", "yum", "target.json"));
        using var doc = JsonDocument.Parse(repoJson);
        Assert.Equal("prod", doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("yum", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("https://repo.example.com/yum", doc.RootElement.GetProperty("destination").GetString());

        var credentialElement = doc.RootElement.GetProperty("credential");
        Assert.Equal("s3", credentialElement.GetProperty("Id").GetString());
        Assert.Equal("token", credentialElement.GetProperty("Type").GetString());
    }

    [Fact]
    public async Task PublishAsync_MissingCredentialProducesError()
    {
        await using var temp = TemporaryDirectoryScope.Create("credential-missing-test");
        var outputDir = temp.DirectoryPath;

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["linux.repo.enabled"] = "true",
            ["linux.repo.targets"] = "secure",
            ["linux.repo.target.secure.type"] = "apt",
            ["linux.repo.target.secure.credential"] = "missing"
        };

        var request = new PackagingRequest("proj", PackagingPlatform.Linux, new[] { "deb" }, "Release", outputDir, properties);
        var project = new PackagingProject(
            "proj",
            "SampleLinux",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new[] { "deb" }, new Dictionary<string, string>())
            });

        var artifactPath = Path.Combine(outputDir, "sample.deb");
        await File.WriteAllTextAsync(artifactPath, "deb");
        var artifact = new PackagingArtifact("deb", artifactPath, new Dictionary<string, string>());

        var publisher = new LinuxRepositoryPublisher(new StubCredentialProvider());
        var issues = await publisher.PublishAsync(new PackageFormatContext(project, request, outputDir), new PackagingResult(true, new[] { artifact }, Array.Empty<PackagingIssue>()));

        var error = Assert.Single(issues, i => i.Code == "linux.repo.credential_missing");
        Assert.Equal(PackagingIssueSeverity.Error, error.Severity);
    }

    private sealed class StubCredentialProvider : ILinuxRepositoryCredentialProvider
    {
        private readonly Dictionary<string, RepositoryCredential?> _credentials = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string id, RepositoryCredential? credential)
        {
            _credentials[id] = credential;
        }

        public Task<RepositoryCredential?> GetCredentialAsync(PackageFormatContext context, string credentialId, CancellationToken cancellationToken = default)
        {
            _credentials.TryGetValue(credentialId, out var credential);
            return Task.FromResult(credential);
        }
    }
}
