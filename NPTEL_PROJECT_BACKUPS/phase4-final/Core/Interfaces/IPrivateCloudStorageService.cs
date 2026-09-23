namespace NPTELManagement.Core.Interfaces;

public interface IPrivateCloudStorageService
{
    bool IsLiveCloudConfigured { get; }
    string ProviderName { get; }
    Task EnsurePrivateBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);
    Task<string> UploadCertificateAsync(string bucketName, string objectPath, Stream fileStream, string contentType, CancellationToken cancellationToken = default);
    Task<string> CreateSignedUrlAsync(string bucketName, string objectPath, int expiresInSeconds = 900, CancellationToken cancellationToken = default);
    Task<bool> DeleteCertificateAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default);
    Task<bool> ObjectExistsAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default);
}
