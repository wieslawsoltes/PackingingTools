using System;
using System.Collections.Generic;

namespace PackagingTools.Core.Security;

/// <summary>
/// Metadata describing a secure store entry without exposing the secret payload.
/// </summary>
/// <param name="Id">Unique identifier for the stored secret.</param>
/// <param name="CreatedAt">Timestamp when the secret was persisted.</param>
/// <param name="ExpiresAt">Optional expiration timestamp.</param>
/// <param name="Metadata">User-defined metadata.</param>
public sealed record SecureStoreEntry(
    string Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    IReadOnlyDictionary<string, string> Metadata);
