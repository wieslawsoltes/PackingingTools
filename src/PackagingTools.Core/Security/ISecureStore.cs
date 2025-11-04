using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Security;

/// <summary>
/// Provides encrypted at-rest storage for sensitive files and secrets.
/// </summary>
public interface ISecureStore
{
    Task<SecureStoreEntry> PutAsync(
        string id,
        ReadOnlyMemory<byte> data,
        SecureStoreEntryOptions options,
        CancellationToken cancellationToken = default);

    Task<SecureStoreSecret?> TryGetAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SecureStoreEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for storing a secret entry.
/// </summary>
/// <param name="ExpiresAt">Optional expiration timestamp used for rotation reminders.</param>
/// <param name="Metadata">Arbitrary metadata captured with the entry.</param>
public sealed record SecureStoreEntryOptions(
    DateTimeOffset? ExpiresAt = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
