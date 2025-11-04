using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml.Linq;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Utilities;
using PackagingTools.Core.Windows.Formats;
using PackagingTools.Core.Windows.Pipelines;
using PackagingTools.Core.Windows.Tooling;
using PackagingTools.Core.Windows.Signing;
using PackagingTools.Core.Windows.Signing.Azure;
using PackagingTools.Core.Mac.Formats;
using PackagingTools.Core.Mac.Pipelines;
using PackagingTools.Core.Mac.Signing;
using PackagingTools.Core.Mac.Tooling;
using PackagingTools.Core.Mac.Verification;
using PackagingTools.Core.Mac.Audit;
using PackagingTools.Core.Linux.Container;
using PackagingTools.Core.Linux.Formats;
using PackagingTools.Core.Linux.Pipelines;
using PackagingTools.Core.Linux.Tooling;
using PackagingTools.Core.Linux.Sandbox;
using PackagingTools.Core.Linux.Repos;
using PackagingTools.Core.Security;
using PackagingTools.Core.Security.Sbom;
using PackagingTools.Core.Security.Vulnerability;
using PackagingTools.Core.Security.Vulnerability.Scanners;
using PackagingTools.Core.Security.Identity;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class PipelineIntegrationTests : IDisposable
{
    private readonly string _tempRoot;

    public PipelineIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PackagingToolsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task WindowsPipeline_PackagesMsixAndMsi()
    {
        var projectId = "sample.windows";
        var payloadDir = CreateSampleWindowsPayload();
        var outputDir = Path.Combine(_tempRoot, "windows-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "SampleWinApp",
            "1.2.3.4",
            new Dictionary<string, string>
            {
                ["windows.identityName"] = "Contoso.SampleWinApp",
                ["windows.publisher"] = "CN=Contoso",
                ["windows.displayName"] = "Sample Windows App",
                ["windows.msix.executable"] = "Sample.exe",
                ["windows.msix.entryPoint"] = "Sample.App",
                ["windows.msix.logo"] = "Assets\\Square150x150Logo.png",
                ["windows.msi.productCode"] = Guid.NewGuid().ToString("B"),
                ["windows.msi.upgradeCode"] = Guid.NewGuid().ToString("B"),
                ["windows.publisher"] = "Contoso"
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new []{ "msix", "msi" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Windows,
            new []{ "msix", "msi" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["windows.msix.payloadDirectory"] = payloadDir,
                ["windows.msi.sourceDirectory"] = payloadDir,
                ["windows.msi.shortcutName"] = "Sample Windows App",
                ["windows.msi.shortcutTarget"] = "Sample.exe",
                ["windows.msi.shortcutDescription"] = "Launch Sample Windows App",
                ["windows.msi.shortcutIcon"] = @"Assets\Square150x150Logo.png",
                ["windows.msi.protocolName"] = "sampleapp",
                ["windows.msi.protocolDisplayName"] = "Sample Windows App Protocol",
                ["windows.msi.protocolCommand"] = "Sample.exe \"%1\"",
                ["windows.msi.shellExtensionExtension"] = ".samplepkg",
                ["windows.msi.shellExtensionProgId"] = "Contoso.SampleWinApp.Document",
                ["windows.msi.shellExtensionDescription"] = "Sample Windows Document",
                ["windows.msi.shellExtensionCommand"] = "Sample.exe \"%1\""
            });

        var processRunner = new StubWindowsProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();

        var providers = new IPackageFormatProvider[]
        {
            new MsixPackageFormatProvider(processRunner, signingService, telemetry, NullLogger<MsixPackageFormatProvider>.Instance),
            new MsiPackageFormatProvider(processRunner, signingService, telemetry, NullLogger<MsiPackageFormatProvider>.Instance)
        };

        var pipeline = new WindowsPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            new TestIdentityContextAccessor(),
            NullLogger<WindowsPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.DoesNotContain(result.Issues, i => i.Severity == PackagingIssueSeverity.Error);
        Assert.True(result.Success);
        Assert.Contains(result.Artifacts, a => a.Format == "msix");
        Assert.Contains(result.Artifacts, a => a.Format == "msi");
        var wixSource = result.Artifacts.First(a => a.Format == "msi").Metadata["wixSource"];
        var wixContent = XDocument.Load(wixSource);
        var wixNs = XNamespace.Get("http://wixtoolset.org/schemas/v4/wxs");

        var package = wixContent.Root?.Element(wixNs + "Package");
        Assert.NotNull(package);
        Assert.Equal("SampleWinApp", package!.Attribute("Name")?.Value);
        Assert.Equal("Contoso", package.Attribute("Manufacturer")?.Value);

        var feature = package.Element(wixNs + "Feature");
        Assert.NotNull(feature);
        var featureRefs = feature!.Elements(wixNs + "ComponentGroupRef")
            .Select(e => e.Attribute("Id")?.Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("ProductComponents", featureRefs);
        Assert.Contains("ProductShortcuts", featureRefs);
        Assert.Contains("ProductProtocol", featureRefs);
        Assert.Contains("ProductFileAssociations", featureRefs);

        var shortcut = wixContent.Descendants(wixNs + "Shortcut")
            .Single(e => e.Attribute("Id")?.Value == "ApplicationShortcut");
        Assert.Equal("Sample Windows App", shortcut.Attribute("Name")?.Value);
        Assert.Equal("[INSTALLFOLDER]Sample.exe", shortcut.Attribute("Target")?.Value);
        Assert.Equal(@"[INSTALLFOLDER]Assets\Square150x150Logo.png", shortcut.Attribute("Icon")?.Value);

        var protocolComponent = wixContent.Descendants(wixNs + "Component")
            .Single(e => e.Attribute("Id")?.Value == "ProtocolComponent");
        Assert.Contains(
            protocolComponent.Descendants(wixNs + "RegistryKey")
                .Where(k => k.Attribute("Key")?.Value == "shell/open/command")
                .SelectMany(k => k.Elements(wixNs + "RegistryValue"))
                .Select(v => v.Attribute("Value")?.Value ?? string.Empty),
            v => v.Contains("[INSTALLFOLDER]Sample.exe", StringComparison.Ordinal));

        var fileAssociationComponent = wixContent.Descendants(wixNs + "Component")
            .Single(e => e.Attribute("Id")?.Value == "FileAssociationComponent");
        Assert.Contains(
            fileAssociationComponent.Descendants(wixNs + "RegistryKey")
                .Where(k => k.Attribute("Key")?.Value == "shell/open/command")
                .SelectMany(k => k.Elements(wixNs + "RegistryValue"))
                .Select(v => v.Attribute("Value")?.Value ?? string.Empty),
            v => v.Contains("[INSTALLFOLDER]Sample.exe", StringComparison.Ordinal));

        Assert.Contains(processRunner.Requests, r => r.FileName.EndsWith("makeappx.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(processRunner.Requests, r => r.FileName.EndsWith("heat.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(processRunner.Requests, r => r.FileName.EndsWith("candle.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(processRunner.Requests, r => r.FileName.EndsWith("light.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WindowsPipeline_UsesAzureKeyVaultSignerWhenConfigured()
    {
        var projectId = "sample.windows.keyvault";
        var payloadDir = CreateSampleWindowsPayload();
        var outputDir = Path.Combine(_tempRoot, "windows-azure-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "RemoteSignedApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new []{ "msi" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Windows,
            new []{ "msi" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["windows.msi.sourceDirectory"] = payloadDir,
                ["windows.signing.azureKeyVaultCertificate"] = "contoso-signing",
                ["windows.signing.azureKeyVaultUrl"] = "https://contoso.vault.azure.net"
            });

        var processRunner = new StubWindowsProcessRunner();
        var telemetry = new RecordingTelemetry();
        var keyVaultClient = new StubAzureKeyVaultClient();
        var keyVaultSigner = new AzureKeyVaultSigner(keyVaultClient);
        var signingService = new WindowsSigningService(processRunner, keyVaultSigner);

        var providers = new IPackageFormatProvider[]
        {
            new MsiPackageFormatProvider(processRunner, signingService, telemetry, NullLogger<MsiPackageFormatProvider>.Instance)
        };

        var pipeline = new WindowsPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            new TestIdentityContextAccessor(),
            NullLogger<WindowsPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Single(keyVaultClient.SignedArtifacts);
        var expectedArtifact = Path.Combine(outputDir, "RemoteSignedApp.msi");
        Assert.Equal(expectedArtifact, keyVaultClient.SignedArtifacts[0]);
        Assert.True(File.Exists(Path.ChangeExtension(expectedArtifact, ".stub.sig")));
    }

    [Fact]
    public async Task WindowsPipeline_SmokeTest_RunsRealWiXToolingWhenAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var missingTools = WindowsTestUtilities.FindMissingWiXTools();
        if (missingTools.Count > 0)
        {
            // Running without WiX locally is acceptable; treat as noop on non-packaging machines.
            return;
        }

        var projectId = "sample.windows.smoke";
        var payloadDir = CreateSampleWindowsPayload();
        var outputDir = Path.Combine(_tempRoot, "windows-smoke-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "SampleWinAppSmoke",
            "1.2.3.4",
            new Dictionary<string, string>
            {
                ["windows.identityName"] = "Contoso.SampleWinAppSmoke",
                ["windows.publisher"] = "CN=Contoso",
                ["windows.msix.executable"] = "Sample.exe",
                ["windows.msi.productCode"] = Guid.NewGuid().ToString("B"),
                ["windows.msi.upgradeCode"] = Guid.NewGuid().ToString("B")
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new []{ "msi" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Windows,
            new []{ "msi" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["windows.msi.sourceDirectory"] = payloadDir,
                ["windows.msi.shortcutName"] = "Sample Windows App Smoke",
                ["windows.msi.shortcutTarget"] = "Sample.exe"
            });

        var processRunner = new ProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();

        var providers = new IPackageFormatProvider[]
        {
            new MsiPackageFormatProvider(processRunner, signingService, telemetry, NullLogger<MsiPackageFormatProvider>.Instance)
        };

        var pipeline = new WindowsPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            new TestIdentityContextAccessor(),
            NullLogger<WindowsPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        var artifact = Assert.Single(result.Artifacts, a => a.Format == "msi");
        Assert.True(File.Exists(artifact.Path));
    }

    [Fact]
    public async Task WindowsPipeline_WritesDiagnosticsWhenToolFails()
    {
        var projectId = "sample.windows.fail";
        var payloadDir = CreateSampleWindowsPayload();
        var outputDir = Path.Combine(_tempRoot, "windows-fail-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "SampleWinAppFail",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new[] { "msi" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Windows,
            new[] { "msi" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["windows.msi.sourceDirectory"] = payloadDir
            });

        var processRunner = new FailingWindowsProcessRunner("heat.exe");
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();

        var pipeline = new WindowsPackagingPipeline(
            new InMemoryProjectStore(project),
            new IPackageFormatProvider[]
            {
                new MsiPackageFormatProvider(processRunner, signingService, telemetry, NullLogger<MsiPackageFormatProvider>.Instance)
            },
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            new TestIdentityContextAccessor(),
            NullLogger<WindowsPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues, i => i.Code == "windows.msi.heat_failed");
        Assert.Contains("_diagnostics", issue.Message, StringComparison.OrdinalIgnoreCase);

        var diagnosticsDir = Path.Combine(outputDir, "_diagnostics");
        Assert.True(Directory.Exists(diagnosticsDir));
        var logFile = Assert.Single(Directory.EnumerateFiles(diagnosticsDir));
        var logContent = await File.ReadAllTextAsync(logFile);
        Assert.Contains("heat.exe", logContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExitCode: 42", logContent, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            telemetry.Events,
            e => e.Event == "windows.tool.failure"
                 && e.Properties is { } props
                 && props.TryGetValue("tool", out var tool)
                 && string.Equals(tool?.ToString(), "heat.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MacPipeline_PackagesBundlePkgAndDmg()
    {
        var projectId = "sample.macos";
        var bundleSource = CreateSampleMacBundle();
        var outputDir = Path.Combine(_tempRoot, "mac-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "SampleMacApp",
            "1.0.0",
            new Dictionary<string, string>
            {
                ["mac.bundleId"] = "com.contoso.macapp",
                ["mac.signing.identity"] = "Developer ID Application: Contoso" ,
                ["mac.pkg.installLocation"] = "/Applications"
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.MacOS] = new(new []{ "app", "pkg", "dmg" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.MacOS,
            new []{ "app", "pkg", "dmg" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["mac.app.bundleSource"] = bundleSource,
                ["mac.pkg.component"] = bundleSource,
                ["mac.dmg.sourceDirectory"] = bundleSource,
                ["mac.signing.identity"] = "Developer ID Application: Contoso"
            });

        var processRunner = new StubMacProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();

        var signingMaterials = new MacSigningMaterialService(
            new FileSecureStore(Path.Combine(_tempRoot, "mac-secure")),
            telemetry,
            NullLogger<MacSigningMaterialService>.Instance);

        var providers = new IPackageFormatProvider[]
        {
            new AppBundleFormatProvider(processRunner, signingService, telemetry, signingMaterials, NullLogger<AppBundleFormatProvider>.Instance),
            new PkgFormatProvider(processRunner, signingService, telemetry, NullLogger<PkgFormatProvider>.Instance),
            new DmgFormatProvider(processRunner, telemetry, NullLogger<DmgFormatProvider>.Instance)
        };

        var pipeline = new MacPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            NoopVerificationService.Instance,
            new AuditIntegrationService(Array.Empty<IMacAuditService>()),
            new TestIdentityContextAccessor(),
            NullLogger<MacPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.DoesNotContain(result.Issues, i => i.Severity == PackagingIssueSeverity.Error);
        Assert.True(result.Success);
        Assert.Contains(result.Artifacts, a => a.Format == "app");
        Assert.Contains(result.Artifacts, a => a.Format == "pkg");
        Assert.Contains(result.Artifacts, a => a.Format == "dmg");
        Assert.Contains(signingService.Requests, r => r.Format == "app");
    }

    [Fact]
    public async Task MacPipeline_UsesSecureStoreSigningMaterials()
    {
        var projectId = "sample.macos.secure";
        var bundleSource = CreateSampleMacBundle();
        var outputDir = Path.Combine(_tempRoot, "mac-secure-output");
        Directory.CreateDirectory(outputDir);

        var secureStorePath = Path.Combine(_tempRoot, "secure-store");
        var telemetry = new RecordingTelemetry();
        var signingMaterials = new MacSigningMaterialService(
            new FileSecureStore(secureStorePath),
            telemetry,
            NullLogger<MacSigningMaterialService>.Instance);

        await signingMaterials.StoreEntitlementsAsync(
            "entitlements-dev",
            System.Text.Encoding.UTF8.GetBytes("<plist></plist>"),
            DateTimeOffset.UtcNow.AddDays(10));

        await signingMaterials.StoreProvisioningProfileAsync(
            "profile-dev",
            System.Text.Encoding.UTF8.GetBytes("profile"),
            DateTimeOffset.UtcNow.AddDays(5));

        var project = new PackagingProject(
            projectId,
            "SecureMacApp",
            "1.2.0",
            new Dictionary<string, string>
            {
                ["mac.bundleId"] = "com.contoso.secureapp",
                ["mac.signing.identity"] = "Developer ID Application: Contoso",
                [MacSigningMaterialService.EntitlementsMetadataKey] = "entitlements-dev",
                [MacSigningMaterialService.ProvisioningMetadataKey] = "profile-dev"
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.MacOS] = new(new []{ "app" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.MacOS,
            new []{ "app" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["mac.app.bundleSource"] = bundleSource
            });

        var processRunner = new StubMacProcessRunner();
        var signingService = new StubSigningService();

        var providers = new IPackageFormatProvider[]
        {
            new AppBundleFormatProvider(processRunner, signingService, telemetry, signingMaterials, NullLogger<AppBundleFormatProvider>.Instance)
        };

        var pipeline = new MacPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            NoopVerificationService.Instance,
            new AuditIntegrationService(Array.Empty<IMacAuditService>()),
            new TestIdentityContextAccessor(),
            NullLogger<MacPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains(result.Issues, i => i.Code == "mac.entitlements.rotation_due" && i.Severity == PackagingIssueSeverity.Warning);
        Assert.Contains(result.Issues, i => i.Code == "mac.provisioning.rotation_due" && i.Severity == PackagingIssueSeverity.Warning);

        var artifact = Assert.Single(result.Artifacts);
        var provisioningProfile = Path.Combine(artifact.Path, "Contents", "embedded.provisionprofile");
        Assert.True(File.Exists(provisioningProfile));

        var signingRequest = Assert.Single(signingService.Requests, r => r.Format == "app");
        Assert.NotNull(signingRequest.Properties);
        var signingProps = signingRequest.Properties!;
        var entitlementsPathValue = Assert.IsType<string>(signingProps["mac.signing.entitlements"]);
        Assert.Contains("entitlements-dev", entitlementsPathValue, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(telemetry.Events, e => e.Event == "mac.signing.material.materialized");
    }

    [Fact]
    public async Task MacPipeline_VerifiesArtifactsWhenEnabled()
    {
        var projectId = "sample.macos.verify";
        var bundleSource = CreateSampleMacBundle();
        var outputDir = Path.Combine(_tempRoot, "mac-verify-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "VerifyMacApp",
            "1.0.0",
            new Dictionary<string, string>
            {
                ["mac.bundleId"] = "com.contoso.verifymac",
                ["mac.signing.identity"] = "Developer ID Application: Contoso"
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.MacOS] = new(new []{ "app", "pkg", "dmg" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.MacOS,
            new []{ "app", "pkg", "dmg" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["mac.app.bundleSource"] = bundleSource,
                ["mac.pkg.component"] = bundleSource,
                ["mac.dmg.sourceDirectory"] = bundleSource,
                ["mac.verify.enabled"] = "true"
            });

        var processRunner = new StubMacProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();
        var signingMaterials = new MacSigningMaterialService(
            new FileSecureStore(Path.Combine(_tempRoot, "mac-secure-verify")),
            telemetry,
            NullLogger<MacSigningMaterialService>.Instance);
        var verification = new RecordingVerificationService();

        var providers = new IPackageFormatProvider[]
        {
            new AppBundleFormatProvider(processRunner, signingService, telemetry, signingMaterials, NullLogger<AppBundleFormatProvider>.Instance),
            new PkgFormatProvider(processRunner, signingService, telemetry, NullLogger<PkgFormatProvider>.Instance),
            new DmgFormatProvider(processRunner, telemetry, NullLogger<DmgFormatProvider>.Instance)
        };

        var pipeline = new MacPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            verification,
            new AuditIntegrationService(Array.Empty<IMacAuditService>()),
            new TestIdentityContextAccessor(),
            NullLogger<MacPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(3, verification.Verifications.Count);
        Assert.Contains(verification.Verifications, v => v.Format == "app");
        Assert.Contains(verification.Verifications, v => v.Format == "pkg");
        Assert.Contains(verification.Verifications, v => v.Format == "dmg");
    }

    [Fact]
    public async Task MacPipeline_CapturesAuditWhenEnabled()
    {
        var projectId = "sample.macos.audit";
        var bundleSource = CreateSampleMacBundle();
        var outputDir = Path.Combine(_tempRoot, "mac-audit-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "AuditMacApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.MacOS] = new(new []{ "app" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.MacOS,
            new []{ "app" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["mac.app.bundleSource"] = bundleSource,
                ["mac.audit.enabled"] = "true"
            });

        var processRunner = new StubMacProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();
        var signingMaterials = new MacSigningMaterialService(
            new FileSecureStore(Path.Combine(_tempRoot, "mac-secure-audit")),
            telemetry,
            NullLogger<MacSigningMaterialService>.Instance);
        var verification = NoopVerificationService.Instance;
        var recordingAudit = new RecordingAuditService();
        var auditIntegration = new AuditIntegrationService(new[] { recordingAudit });

        var providers = new IPackageFormatProvider[]
        {
            new AppBundleFormatProvider(processRunner, signingService, telemetry, signingMaterials, NullLogger<AppBundleFormatProvider>.Instance)
        };

        var pipeline = new MacPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            verification,
            auditIntegration,
            new TestIdentityContextAccessor(),
            NullLogger<MacPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(1, recordingAudit.Invocations);
    }

    [Fact]
    public async Task LinuxPipeline_PackagesMultipleFormats()
    {
        var projectId = "sample.linux";
        var payloadDir = CreateSampleLinuxPayload();
        var manifestPath = Path.Combine(_tempRoot, "flatpak-manifest.json");
        await File.WriteAllTextAsync(manifestPath, "{}");
        var snapManifest = Path.Combine(_tempRoot, "snapcraft.yaml");
        await File.WriteAllTextAsync(snapManifest, "name: sample\nversion: 1.0.0\nparts:\n  app:\n    plugin: nil\n");

        var outputDir = Path.Combine(_tempRoot, "linux-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "SampleLinuxApp",
            "1.0.0",
            new Dictionary<string, string>
            {
                ["linux.architecture"] = "amd64",
                ["linux.deb.description"] = "Sample app",
                ["linux.deb.maintainer"] = "ops@contoso.com",
                ["linux.rpm.description"] = "Sample app"
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new []{ "deb", "rpm", "appimage", "flatpak", "snap" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Linux,
            new []{ "deb", "rpm", "appimage", "flatpak", "snap" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["linux.packageRoot"] = payloadDir,
                ["linux.appimage.appDir"] = payloadDir,
                ["linux.flatpak.manifest"] = manifestPath,
                ["linux.snap.manifest"] = snapManifest
            });

        var processRunner = new StubLinuxProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();
        var sandboxService = new LinuxSandboxProfileService(NullLogger<LinuxSandboxProfileService>.Instance);
        var repoPublisher = new LinuxRepositoryPublisher(new PropertyLinuxRepositoryCredentialProvider(), NullLogger<LinuxRepositoryPublisher>.Instance);
        var containerService = new DockerLinuxContainerBuildService(NullLogger<DockerLinuxContainerBuildService>.Instance);

        var providers = new IPackageFormatProvider[]
        {
            new DebFormatProvider(processRunner, telemetry, NullLogger<DebFormatProvider>.Instance),
            new RpmFormatProvider(processRunner, telemetry, NullLogger<RpmFormatProvider>.Instance),
            new AppImageFormatProvider(processRunner, telemetry, NullLogger<AppImageFormatProvider>.Instance),
            new FlatpakFormatProvider(processRunner, telemetry, NullLogger<FlatpakFormatProvider>.Instance),
            new SnapFormatProvider(processRunner, telemetry, NullLogger<SnapFormatProvider>.Instance)
        };

        var pipeline = new LinuxPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            sandboxService,
            repoPublisher,
            new[] { NullSbomGenerator.Instance },
            new[] { NullVulnerabilityScanner.Instance },
            containerService,
            new TestIdentityContextAccessor(),
            NullLogger<LinuxPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains(result.Artifacts, a => a.Format == "deb");
        Assert.Contains(result.Artifacts, a => a.Format == "rpm");
        Assert.Contains(result.Artifacts, a => a.Format == "appimage");
        Assert.Contains(result.Artifacts, a => a.Format == "flatpak");
        Assert.Contains(result.Artifacts, a => a.Format == "snap");
        Assert.Contains(processRunner.Requests, r => r.FileName == "fpm");
        Assert.Contains(processRunner.Requests, r => r.FileName == "flatpak-builder");
        Assert.Contains(processRunner.Requests, r => r.FileName == "snapcraft");
    }

    [Fact]
    public async Task LinuxPipeline_CapturesSandboxProfiles()
    {
        var projectId = "sample.linux.sandbox";
        var payloadDir = CreateSampleLinuxPayload();
        var outputDir = Path.Combine(_tempRoot, "linux-sandbox-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "SandboxedApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new []{ "appimage" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Linux,
            new []{ "appimage" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["linux.sandbox.enabled"] = "true",
                ["linux.sandbox.apparmorProfile"] = "usr.bin.sample",
                ["linux.sandbox.selinuxContext"] = "system_u:system_r:sample_t:s0",
                ["linux.sandbox.flatpakPermissions"] = "filesystem=home;socket=x11",
                ["linux.packageRoot"] = payloadDir,
                ["linux.appimage.appDir"] = payloadDir
            });

        var processRunner = new StubLinuxProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();
        var sandboxService = new LinuxSandboxProfileService(NullLogger<LinuxSandboxProfileService>.Instance);
        var repoPublisher = new LinuxRepositoryPublisher(new PropertyLinuxRepositoryCredentialProvider(), NullLogger<LinuxRepositoryPublisher>.Instance);
        var containerService = new DockerLinuxContainerBuildService(NullLogger<DockerLinuxContainerBuildService>.Instance);

        var providers = new IPackageFormatProvider[]
        {
            new AppImageFormatProvider(processRunner, telemetry, NullLogger<AppImageFormatProvider>.Instance)
        };

        var pipeline = new LinuxPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            sandboxService,
            repoPublisher,
            new[] { NullSbomGenerator.Instance },
            new[] { NullVulnerabilityScanner.Instance },
            containerService,
            new TestIdentityContextAccessor(),
            NullLogger<LinuxPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        var auditDir = Path.Combine(outputDir, "_Audit", "sandbox");
        Assert.True(Directory.Exists(auditDir));
        Assert.Contains(Directory.EnumerateFiles(auditDir, "profile.json", SearchOption.AllDirectories), _ => true);
    }

    private string CreateSampleWindowsPayload()
    {
        var dir = Path.Combine(_tempRoot, "win-payload");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "Assets"));
        File.WriteAllText(Path.Combine(dir, "Sample.exe"), string.Empty);
        File.WriteAllBytes(Path.Combine(dir, "Assets", "Square150x150Logo.png"), new byte[]{1,2,3});
        return dir;
    }

    private string CreateSampleMacBundle()
    {
        var dir = Path.Combine(_tempRoot, "mac-app", "SampleMacApp.app");
        Directory.CreateDirectory(Path.Combine(dir, "Contents", "MacOS"));
        Directory.CreateDirectory(Path.Combine(dir, "Contents", "Resources"));
        File.WriteAllText(Path.Combine(dir, "Contents", "MacOS", "App"), "#!/bin/bash");
        File.WriteAllText(Path.Combine(dir, "Contents", "Info.plist"), "<plist></plist>");
        return dir;
    }

    private string CreateSampleLinuxPayload()
    {
        var dir = Path.Combine(_tempRoot, "linux-root");
        Directory.CreateDirectory(Path.Combine(dir, "usr", "bin"));
        Directory.CreateDirectory(Path.Combine(dir, "usr", "share", "applications"));
        File.WriteAllText(Path.Combine(dir, "usr", "bin", "sample"), "#!/bin/bash");
        File.WriteAllText(Path.Combine(dir, "usr", "share", "applications", "sample.desktop"), "[Desktop Entry]");
        return dir;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private sealed class StubWindowsProcessRunner : IProcessRunner
    {
        public List<ProcessExecutionRequest> Requests { get; } = new();

        public Task<ProcessExecutionResult> ExecuteAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            if (request.FileName.EndsWith("makeappx.exe", StringComparison.OrdinalIgnoreCase))
            {
                var outputPath = ExtractQuotedArgument(request.Arguments, "/p");
                if (!string.IsNullOrEmpty(outputPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    File.WriteAllText(outputPath, "msix");
                }
            }
            else if (request.FileName.EndsWith("heat.exe", StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.Combine(request.WorkingDirectory ?? Environment.CurrentDirectory, "Harvested.wxs");
                File.WriteAllText(path, "<Wix></Wix>");
            }
            else if (request.FileName.EndsWith("candle.exe", StringComparison.OrdinalIgnoreCase))
            {
                var outputPath = ExtractQuotedArgument(request.Arguments, "-o");
                if (!string.IsNullOrEmpty(outputPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    File.WriteAllText(outputPath, "wixobj");
                }
            }
            else if (request.FileName.EndsWith("light.exe", StringComparison.OrdinalIgnoreCase))
            {
                var outputPath = ExtractQuotedArgument(request.Arguments, "-o");
                if (!string.IsNullOrEmpty(outputPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    File.WriteAllText(outputPath, "msi");
                }
            }

            return Task.FromResult(new ProcessExecutionResult(0, "ok", string.Empty));
        }

        private static string? ExtractQuotedArgument(string arguments, string token)
        {
            var index = arguments.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var startQuote = arguments.IndexOf('"', index + token.Length);
            if (startQuote < 0)
            {
                return null;
            }

            var endQuote = arguments.IndexOf('"', startQuote + 1);
            if (endQuote < 0)
            {
                return null;
            }

            return arguments.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
    }

    private sealed class FailingWindowsProcessRunner : IProcessRunner
    {
        private readonly string _toolName;
        private readonly int _exitCode;

        public FailingWindowsProcessRunner(string toolName, int exitCode = 42)
        {
            _toolName = toolName;
            _exitCode = exitCode;
        }

        public Task<ProcessExecutionResult> ExecuteAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.FileName.EndsWith(_toolName, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new ProcessExecutionResult(_exitCode, string.Empty, $"{_toolName} failed"));
            }

            return Task.FromResult(new ProcessExecutionResult(0, "ok", string.Empty));
        }
    }

    private sealed class StubMacProcessRunner : IMacProcessRunner
    {
        public List<MacProcessRequest> Requests { get; } = new();

        public Task<MacProcessResult> ExecuteAsync(MacProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            if (request.FileName == "productbuild")
            {
                var output = request.Arguments.Last();
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "pkg");
            }
            else if (request.FileName == "hdiutil")
            {
                var output = request.Arguments.First(a => a.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "dmg");
            }

            return Task.FromResult(new MacProcessResult(0, "ok", string.Empty));
        }
    }

    private sealed class StubLinuxProcessRunner : ILinuxProcessRunner
    {
        public List<LinuxProcessRequest> Requests { get; } = new();

        public Task<LinuxProcessResult> ExecuteAsync(LinuxProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            switch (request.FileName)
            {
                case "fpm":
                {
                    var outputIndex = request.Arguments.ToList().FindIndex(a => a == "--output");
                    if (outputIndex >= 0 && outputIndex + 1 < request.Arguments.Count)
                    {
                        var output = request.Arguments[outputIndex + 1];
                        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                        File.WriteAllText(output, "package");
                    }
                    break;
                }
                case "appimagetool":
                {
                    var output = request.Arguments.Last();
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    File.WriteAllText(output, "appimage");
                    break;
                }
                case "flatpak-builder":
                {
                    var repoIndex = request.Arguments.ToList().FindIndex(a => a == "--repo");
                    if (repoIndex >= 0 && repoIndex + 1 < request.Arguments.Count)
                    {
                        Directory.CreateDirectory(request.Arguments[repoIndex + 1]);
                    }
                    break;
                }
                case "snapcraft":
                {
                    var workingDir = request.WorkingDirectory ?? Environment.CurrentDirectory;
                    File.WriteAllText(Path.Combine(workingDir, "SampleLinuxApp_1.0.0_amd64.snap"), "snap");
                    break;
                }
            }

            return Task.FromResult(new LinuxProcessResult(0, "ok", string.Empty));
        }
    }

    private sealed class StubSbomGenerator : ISbomGenerator
    {
        private readonly string _path;
        private readonly PackagingIssue? _issue;

        public int CallCount { get; private set; }

        public StubSbomGenerator(string path, PackagingIssue? issue = null)
        {
            _path = path;
            _issue = issue;
        }

        public string Format => "stub";

        public Task<SbomGenerationResult> GenerateAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SbomGenerationResult(_path, _issue));
        }
    }

    private sealed class StubVulnerabilityScanner : IVulnerabilityScanner
    {
        private readonly VulnerabilityFinding[] _findings;
        private readonly PackagingIssue? _issue;

        public int CallCount { get; private set; }

        public StubVulnerabilityScanner(VulnerabilityFinding[] findings, PackagingIssue? issue = null)
        {
            _findings = findings;
            _issue = issue;
        }

        public string Name => "stub";

        public Task<VulnerabilityScanResult> ScanAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new VulnerabilityScanResult(_findings, _issue));
        }
    }

    private sealed class StubContainerBuildService : ILinuxContainerBuildService
    {
        public Task<IReadOnlyCollection<PackagingIssue>> GenerateAsync(PackagingProject project, PackagingRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<PackagingIssue>>(Array.Empty<PackagingIssue>());
    }

    private sealed class NoopVerificationService : IMacVerificationService
    {
        public static NoopVerificationService Instance { get; } = new();

        public Task<MacVerificationResult> VerifyAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default)
            => Task.FromResult(MacVerificationResult.Succeeded());
    }

    private sealed class RecordingVerificationService : IMacVerificationService
    {
        public List<PackagingArtifact> Verifications { get; } = new();

        public Task<MacVerificationResult> VerifyAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default)
        {
            Verifications.Add(artifact);
            return Task.FromResult(MacVerificationResult.Succeeded());
        }
    }

    private sealed class RecordingAuditService : IMacAuditService
    {
        public int Invocations { get; private set; }

        public Task<IReadOnlyCollection<PackagingIssue>> CaptureAsync(PackageFormatContext context, PackagingResult result, CancellationToken cancellationToken = default)
        {
            Invocations++;
            return Task.FromResult<IReadOnlyCollection<PackagingIssue>>(Array.Empty<PackagingIssue>());
        }
    }

    [Fact]
    public async Task LinuxPipeline_WritesRepositoryManifests()
    {
        var projectId = "sample.linux.repo";
        var payloadDir = CreateSampleLinuxPayload();
        var outputDir = Path.Combine(_tempRoot, "linux-repo-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "RepoApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new []{ "deb" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Linux,
            new []{ "deb" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["linux.sandbox.enabled"] = "false",
                ["linux.repo.enabled"] = "true",
                ["linux.repo.targets"] = "stable",
                ["linux.repo.target.stable.type"] = "apt",
                ["linux.repo.target.stable.destination"] = "/var/repos/apt",
                ["linux.repo.target.stable.components"] = "main",
                ["linux.packageRoot"] = payloadDir
            });

        var processRunner = new StubLinuxProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();
        var sandboxService = new LinuxSandboxProfileService(NullLogger<LinuxSandboxProfileService>.Instance);
        var repoPublisher = new LinuxRepositoryPublisher(new PropertyLinuxRepositoryCredentialProvider(), NullLogger<LinuxRepositoryPublisher>.Instance);
        var containerService = new DockerLinuxContainerBuildService(NullLogger<DockerLinuxContainerBuildService>.Instance);

        var providers = new IPackageFormatProvider[]
        {
            new DebFormatProvider(processRunner, telemetry, NullLogger<DebFormatProvider>.Instance)
        };

        var pipeline = new LinuxPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            sandboxService,
            repoPublisher,
            new[] { NullSbomGenerator.Instance },
            new[] { NullVulnerabilityScanner.Instance },
            containerService,
            new TestIdentityContextAccessor(),
            NullLogger<LinuxPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        var packagesPath = Path.Combine(outputDir, "_Repo", "stable", "apt", "dists", "stable", "main", "binary-amd64", "Packages");
        Assert.True(File.Exists(packagesPath));
    }

    [Fact]
    public async Task LinuxPipeline_ReportsSecurityIssues()
    {
        var projectId = "sample.linux.security";
        var payloadDir = CreateSampleLinuxPayload();
        var outputDir = Path.Combine(_tempRoot, "linux-security-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "SecurityApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new []{ "deb" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Linux,
            new []{ "deb" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["linux.packageRoot"] = payloadDir,
                ["security.sbom.enabled"] = "true",
                ["security.sbom.format"] = "stub",
                ["security.vuln.enabled"] = "true",
                ["security.vuln.provider"] = "stub"
            });

        var processRunner = new StubLinuxProcessRunner();
        var telemetry = new RecordingTelemetry();
        var sandboxService = new LinuxSandboxProfileService(NullLogger<LinuxSandboxProfileService>.Instance);
        var repoPublisher = new LinuxRepositoryPublisher(new PropertyLinuxRepositoryCredentialProvider(), NullLogger<LinuxRepositoryPublisher>.Instance);
        var sbomGenerator = new StubSbomGenerator(Path.Combine(outputDir, "_Sbom", "sample.cdx.json"));
        var vulnScanner = new StubVulnerabilityScanner(new[]
        {
            new VulnerabilityFinding("CVE-2024-1234", "High", "Sample vulnerability", "https://example.com/cve-2024-1234")
        });
        var containerService = new StubContainerBuildService();

        var providers = new IPackageFormatProvider[]
        {
            new DebFormatProvider(processRunner, telemetry, NullLogger<DebFormatProvider>.Instance)
        };

        var pipeline = new LinuxPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            sandboxService,
            repoPublisher,
            new[] { sbomGenerator },
            new[] { vulnScanner },
            containerService,
            new TestIdentityContextAccessor(),
            NullLogger<LinuxPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.True(request.Properties != null && request.Properties.ContainsKey("security.sbom.enabled"));
        Assert.True(request.Properties != null && request.Properties.ContainsKey("security.vuln.enabled"));
        Assert.NotEmpty(result.Artifacts);
        foreach (var issue in result.Issues)
        {
            Console.WriteLine($"SEC ISSUE {issue.Code}: {issue.Message}");
        }
        Assert.True(sbomGenerator.CallCount > 0);
        Assert.True(vulnScanner.CallCount > 0);
        Assert.Contains(result.Issues, i => i.Code == "security.sbom.generated");
        Assert.Contains(result.Issues, i => i.Code.StartsWith("security.vuln", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LinuxPipeline_GeneratesContainerBuildScript()
    {
        var projectId = "sample.linux.container";
        var payloadDir = CreateSampleLinuxPayload();
        var outputDir = Path.Combine(_tempRoot, "linux-container-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            projectId,
            "ContainerApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new []{ "deb" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            projectId,
            PackagingPlatform.Linux,
            new []{ "deb" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["linux.packageRoot"] = payloadDir,
                ["linux.repo.enabled"] = "false",
                ["linux.container.image"] = "mcr.microsoft.com/dotnet/sdk:7.0",
                ["linux.container.projectPath"] = "projects/container-app.json"
            });

        var processRunner = new StubLinuxProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();
        var sandboxService = new LinuxSandboxProfileService(NullLogger<LinuxSandboxProfileService>.Instance);
        var repoPublisher = new LinuxRepositoryPublisher(new PropertyLinuxRepositoryCredentialProvider(), NullLogger<LinuxRepositoryPublisher>.Instance);
        var containerService = new DockerLinuxContainerBuildService(NullLogger<DockerLinuxContainerBuildService>.Instance);

        var providers = new IPackageFormatProvider[]
        {
            new DebFormatProvider(processRunner, telemetry, NullLogger<DebFormatProvider>.Instance)
        };

        var pipeline = new LinuxPackagingPipeline(
            new InMemoryProjectStore(project),
            providers,
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            sandboxService,
            repoPublisher,
            new[] { NullSbomGenerator.Instance },
            new[] { NullVulnerabilityScanner.Instance },
            containerService,
            new TestIdentityContextAccessor(),
            NullLogger<LinuxPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.True(result.Success);
        var scriptPath = Path.Combine(outputDir, "container-build.sh");
        Assert.True(File.Exists(scriptPath));
        Assert.Contains(result.Issues, i => i.Code == "linux.container.script_generated");
    }
}
