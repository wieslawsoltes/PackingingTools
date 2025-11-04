using System;

namespace PackagingTools.Core.Security;

/// <summary>
/// Represents a secure entry decrypted in-memory along with its metadata.
/// </summary>
/// <param name="Entry">Metadata for the secret.</param>
/// <param name="Payload">Decrypted secret payload.</param>
public sealed record SecureStoreSecret(
    SecureStoreEntry Entry,
    ReadOnlyMemory<byte> Payload);
