using System;
using System.Collections.Generic;

namespace PackagingTools.Core.Security.Identity;

/// <summary>
/// Represents an authenticated user or service account.
/// </summary>
public sealed record IdentityPrincipal(
    string Id,
    string DisplayName,
    string? Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyDictionary<string, string> Claims)
{
    public static IdentityPrincipal ServiceAccount { get; } = new(
        Id: "service-account",
        DisplayName: "PackagingTools Service",
        Email: null,
        Roles: Array.Empty<string>(),
        Claims: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
