using Xunit;

namespace PackagingTools.IntegrationTests;

// These tests share static plugin state and must not run concurrently.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PluginTestCollection
{
    public const string Name = "Plugin tests";
}
