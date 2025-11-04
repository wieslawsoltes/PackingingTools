using System.Collections.Generic;

namespace PackagingTools.Core.Linux.Repos;

/// <summary>
/// Represents credential data for repository publishing.
/// </summary>
public sealed record RepositoryCredential(
    string Id,
    string Type,
    IReadOnlyDictionary<string, string> Properties);
