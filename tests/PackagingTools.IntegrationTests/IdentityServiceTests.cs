using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Security;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class IdentityServiceTests
{
    [Fact]
    public async Task LocalIdentityProvider_ReturnsServiceAccount()
    {
        var store = new FileSecureStore(Path.Combine(Path.GetTempPath(), "IdentityTests", Guid.NewGuid().ToString("N")));
        var service = IdentityServiceFactory.CreateDefault(store);
        var request = new IdentityRequest("local", new[] { "packaging" }, false, new Dictionary<string, string>());

        var result = await service.AcquireAsync(request);

        Assert.Equal("service-account", result.Principal.Id);
        Assert.Equal("PackagingTools Service", result.Principal.DisplayName);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task AzureAdIdentityProvider_CachesTokens()
    {
        var store = new FileSecureStore(Path.Combine(Path.GetTempPath(), "IdentityTests", Guid.NewGuid().ToString("N")));
        var service = IdentityServiceFactory.CreateDefault(store);
        var parameters = new Dictionary<string, string>
        {
            ["tenantId"] = "contoso.onmicrosoft.com",
            ["username"] = "build.agent",
            ["roles"] = "ReleaseEngineer"
        };

        var request = new IdentityRequest("azuread", new[] { "packaging.run" }, false, parameters);
        var first = await service.AcquireAsync(request);
        var second = await service.AcquireAsync(request);

        Assert.NotNull(first.AccessToken);
        Assert.Equal(first.AccessToken!.Value, second.AccessToken!.Value);
        Assert.Contains("ReleaseEngineer", second.Principal.Roles);
    }

    [Fact]
    public async Task OktaIdentityProvider_RespectsMfaRequirement()
    {
        var store = new FileSecureStore(Path.Combine(Path.GetTempPath(), "IdentityTests", Guid.NewGuid().ToString("N")));
        var service = IdentityServiceFactory.CreateDefault(store);
        var parameters = new Dictionary<string, string>
        {
            ["domain"] = "dev-123456.okta.com",
            ["username"] = "release.engineer",
            ["roles"] = "ReleaseEngineer,SecurityOfficer",
            ["mfaCode"] = "123456"
        };

        var request = new IdentityRequest("okta", new[] { "packaging.approve" }, true, parameters);
        var result = await service.AcquireAsync(request);

        Assert.NotNull(result.AccessToken);
        Assert.Contains("ReleaseEngineer", result.Principal.Roles);
        Assert.Equal("true", result.Principal.Claims["mfa"]);
    }
}
