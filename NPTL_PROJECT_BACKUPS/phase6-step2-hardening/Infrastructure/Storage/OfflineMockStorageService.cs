using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using NPTELManagement.Core.Interfaces;

namespace NPTELManagement.Infrastructure.Storage;

/// <summary>
/// Dedicated offline test provider used ONLY for isolated unit tests.
/// Explicitly flagged as non-cloud and will NEVER be reported as real Supabase integration.
/// </summary>
public class OfflineMockStorageService : IPrivateCloudStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _storage = new();
    private readonly ConcurrentDictionary<string, string> _tokens = new();

    public bool IsLiveCloudConfigured => false;
    public string ProviderName => "OfflineMockStorage (Non-Cloud Unit Test)";

    public Task EnsurePrivateBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<string> UploadCertificateAsync(string bucketName, string objectPath, Stream fileStream, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, cancellationToken);
        var key = $"{bucketName}/{objectPath.TrimStart('/')}";
        _storage[key] = ms.ToArray();
        return key;
    }

    public Task<string> CreateSignedUrlAsync(string bucketName, string objectPath, int expiresInSeconds = 900, CancellationToken cancellationToken = default)
    {
        var key = objectPath.StartsWith($"{bucketName}/") ? objectPath : $"{bucketName}/{objectPath.TrimStart('/')}";
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{key}:{DateTime.UtcNow.Ticks}"))).ToLowerInvariant();
        _tokens[token] = key;
        var mockUrl = $"http://127.0.0.1:5000/api/v1/certificates/mock-download?token={token}";
        return Task.FromResult(mockUrl);
    }

    public Task<bool> DeleteCertificateAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default)
    {
        var key = objectPath.StartsWith($"{bucketName}/") ? objectPath : $"{bucketName}/{objectPath.TrimStart('/')}";
        return Task.FromResult(_storage.TryRemove(key, out _));
    }

    public Task<bool> ObjectExistsAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default)
    {
        var key = objectPath.StartsWith($"{bucketName}/") ? objectPath : $"{bucketName}/{objectPath.TrimStart('/')}";
        return Task.FromResult(_storage.ContainsKey(key));
    }
}
