using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Configuration;

public static class PackagingProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static async Task<PackagingProject> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<ProjectDocument>(stream, Options, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to parse project file '{path}'.");
        return document.ToModel();
    }

    public static async Task SaveAsync(PackagingProject project, string path, CancellationToken cancellationToken = default)
    {
        var document = ProjectDocument.FromModel(project);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, Options, cancellationToken);
    }

    private sealed record ProjectDocument(string Id, string Name, string Version, Dictionary<string, string> Metadata, Dictionary<string, PlatformDocument> Platforms)
    {
        public PackagingProject ToModel()
        {
            var platformConfigs = Platforms?.ToDictionary(
                kvp => Enum.Parse<PackagingPlatform>(kvp.Key, ignoreCase: true),
                kvp => kvp.Value.ToConfiguration()) ?? new Dictionary<PackagingPlatform, PlatformConfiguration>();

            return new PackagingProject(Id, Name, Version, Metadata ?? new Dictionary<string, string>(), platformConfigs);
        }

        public static ProjectDocument FromModel(PackagingProject project)
        {
            var platforms = project.Platforms.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => PlatformDocument.FromConfiguration(kvp.Value));
            return new ProjectDocument(project.Id, project.Name, project.Version, new Dictionary<string, string>(project.Metadata), platforms);
        }
    }

    private sealed record PlatformDocument(List<string> Formats, Dictionary<string, string> Properties)
    {
        public PlatformConfiguration ToConfiguration()
            => new(Formats ?? new List<string>(), Properties ?? new Dictionary<string, string>());

        public static PlatformDocument FromConfiguration(PlatformConfiguration configuration)
            => new(configuration.Formats.ToList(), new Dictionary<string, string>(configuration.Properties));
    }
}
