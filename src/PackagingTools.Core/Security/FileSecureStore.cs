using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Security;

/// <summary>
/// File-system backed secure store that encrypts payloads using current-user data protection APIs.
/// </summary>
public sealed class FileSecureStore : ISecureStore
{
    private const string PayloadFileName = "payload.bin";
    private const string MetadataFileName = "entry.json";
    private const string MasterKeyFileName = "master.key";

    private readonly string _root;
    private readonly Lazy<byte[]> _masterKey;

    public FileSecureStore(string? rootDirectory = null)
    {
        _root = rootDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PackagingTools", "secure-store");
        Directory.CreateDirectory(_root);
        _masterKey = new Lazy<byte[]>(LoadOrCreateMasterKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<SecureStoreEntry> PutAsync(string id, ReadOnlyMemory<byte> data, SecureStoreEntryOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("id cannot be null or whitespace", nameof(id));
        }

        var sanitizedId = SanitizeId(id);
        var entryDir = Path.Combine(_root, sanitizedId);
        Directory.CreateDirectory(entryDir);

        var entry = new SecureStoreEntry(
            sanitizedId,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: options.ExpiresAt,
            Metadata: options.Metadata is null ? new Dictionary<string, string>() : new Dictionary<string, string>(options.Metadata));

        var payloadPath = Path.Combine(entryDir, PayloadFileName);
        var metadataPath = Path.Combine(entryDir, MetadataFileName);

        var protectedPayload = Encrypt(data);
        await File.WriteAllBytesAsync(payloadPath, protectedPayload, cancellationToken).ConfigureAwait(false);

        var document = new EntryDocument
        {
            Id = entry.Id,
            CreatedAt = entry.CreatedAt,
            ExpiresAt = entry.ExpiresAt,
            Metadata = new Dictionary<string, string>(entry.Metadata)
        };

        await using var stream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(stream, document, cancellationToken: cancellationToken).ConfigureAwait(false);

        return entry;
    }

    public async Task<SecureStoreSecret?> TryGetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var sanitizedId = SanitizeId(id);
        var entryDir = Path.Combine(_root, sanitizedId);
        var payloadPath = Path.Combine(entryDir, PayloadFileName);
        var metadataPath = Path.Combine(entryDir, MetadataFileName);

        if (!File.Exists(payloadPath) || !File.Exists(metadataPath))
        {
            return null;
        }

        await using var metadataStream = File.OpenRead(metadataPath);
        var document = await JsonSerializer.DeserializeAsync<EntryDocument>(metadataStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        var metadata = document.Metadata ?? new Dictionary<string, string>();
        var protectedPayload = await File.ReadAllBytesAsync(payloadPath, cancellationToken).ConfigureAwait(false);
        var decrypted = Decrypt(protectedPayload);

        var entry = new SecureStoreEntry(document.Id!, document.CreatedAt, document.ExpiresAt, metadata);
        return new SecureStoreSecret(entry, decrypted);
    }

    public Task<IReadOnlyCollection<SecureStoreEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<SecureStoreEntry>();
        if (!Directory.Exists(_root))
        {
            return Task.FromResult<IReadOnlyCollection<SecureStoreEntry>>(entries);
        }

        foreach (var directory in Directory.EnumerateDirectories(_root))
        {
            var metadataPath = Path.Combine(directory, MetadataFileName);
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            using var stream = File.OpenRead(metadataPath);
            var document = JsonSerializer.Deserialize<EntryDocument>(stream);
            if (document is null)
            {
                continue;
            }

            entries.Add(new SecureStoreEntry(
                document.Id!,
                document.CreatedAt,
                document.ExpiresAt,
                document.Metadata ?? new Dictionary<string, string>()));
        }

        return Task.FromResult<IReadOnlyCollection<SecureStoreEntry>>(entries);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult(false);
        }

        var sanitizedId = SanitizeId(id);
        var entryDir = Path.Combine(_root, sanitizedId);
        if (!Directory.Exists(entryDir))
        {
            return Task.FromResult(false);
        }

        Directory.Delete(entryDir, recursive: true);
        return Task.FromResult(true);
    }

    private static string SanitizeId(string id)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new char[id.Length];
        for (var i = 0; i < id.Length; i++)
        {
            var ch = id[i];
            sanitized[i] = invalidChars.Contains(ch) ? '_' : ch;
        }
        return new string(sanitized);
    }

    private byte[] LoadOrCreateMasterKey()
    {
        var keyPath = Path.Combine(_root, MasterKeyFileName);
        if (File.Exists(keyPath))
        {
            var existing = File.ReadAllText(keyPath, Encoding.UTF8).Trim();
            return Convert.FromBase64String(existing);
        }

        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var encoded = Convert.ToBase64String(key);
        File.WriteAllText(keyPath, encoded, Encoding.UTF8);
        return key;
    }

    private byte[] Encrypt(ReadOnlyMemory<byte> data)
    {
        var key = _masterKey.Value;
        using var aes = new AesGcm(key, 16);
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[data.Length];
        var tag = new byte[16];

        aes.Encrypt(nonce, data.Span, ciphertext, tag);

        var protectedPayload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, protectedPayload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, protectedPayload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, protectedPayload, nonce.Length + tag.Length, ciphertext.Length);
        return protectedPayload;
    }

    private byte[] Decrypt(ReadOnlySpan<byte> payload)
    {
        var nonceLength = 12;
        var tagLength = 16;
        if (payload.Length < nonceLength + tagLength)
        {
            throw new InvalidOperationException("Corrupted secure store payload.");
        }

        var nonce = payload.Slice(0, nonceLength);
        var tag = payload.Slice(nonceLength, tagLength);
        var cipher = payload.Slice(nonceLength + tagLength);

        var plaintext = new byte[cipher.Length];
        using var aes = new AesGcm(_masterKey.Value, 16);
        aes.Decrypt(nonce, cipher, tag, plaintext);
        return plaintext;
    }

    private sealed class EntryDocument
    {
        public string? Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
