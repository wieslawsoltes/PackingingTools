namespace PackagingTools.Core.Models;

/// <summary>
/// Describes a single packaging run.
/// </summary>
/// <param name="ProjectId">Identifier of the project to package.</param>
/// <param name="Platform">Target platform (Windows/macOS/Linux).</param>
/// <param name="Formats">Requested package formats (e.g. "msix", "msi").</param>
/// <param name="Configuration">Build configuration or channel (e.g. Release).</param>
/// <param name="OutputDirectory">Absolute or project-relative path for produced artifacts.</param>
/// <param name="Properties">Optional additional key/value pairs understood by providers.</param>
public sealed record PackagingRequest(
    string ProjectId,
    PackagingPlatform Platform,
    IReadOnlyCollection<string> Formats,
    string Configuration,
    string OutputDirectory,
    IReadOnlyDictionary<string, string>? Properties = null);
