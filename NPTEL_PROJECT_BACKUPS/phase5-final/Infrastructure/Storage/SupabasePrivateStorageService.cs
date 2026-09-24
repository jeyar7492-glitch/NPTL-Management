using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NPTELManagement.Core.Interfaces;

namespace NPTELManagement.Infrastructure.Storage;

public class SupabasePrivateStorageService : IPrivateCloudStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly string? _storageServiceRoleKey;
    private readonly ILogger<SupabasePrivateStorageService> _logger;

    public bool IsLiveCloudConfigured => !string.IsNullOrWhiteSpace(_supabaseUrl) && 
                                         (!string.IsNullOrWhiteSpace(_serviceKey) || !string.IsNullOrWhiteSpace(_storageServiceRoleKey));
    public string ProviderName => IsLiveCloudConfigured ? "SupabasePrivateCloudStorage" : "SupabasePrivateCloudStorage (Unconfigured)";

    private string? EffectiveStorageJwt
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_storageServiceRoleKey) && 
                _storageServiceRoleKey.StartsWith("eyJ", StringComparison.OrdinalIgnoreCase))
            {
                return _storageServiceRoleKey;
            }

            if (!string.IsNullOrWhiteSpace(_serviceKey) && 
                _serviceKey.StartsWith("eyJ", StringComparison.OrdinalIgnoreCase))
            {
                return _serviceKey;
            }

            return null;
        }
    }

    public SupabasePrivateStorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupabasePrivateStorageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = configuration["SUPABASE_URL"] 
                      ?? configuration["Supabase:Url"]
                      ?? Environment.GetEnvironmentVariable("SUPABASE_URL")
                      ?? Environment.GetEnvironmentVariable("SUPABASE_URL", EnvironmentVariableTarget.User)
                      ?? Environment.GetEnvironmentVariable("SUPABASE_URL", EnvironmentVariableTarget.Machine);

        _serviceKey = configuration["SUPABASE_SERVICE_KEY"] 
                     ?? configuration["SUPABASE_SERVICE_ROLE_KEY"] 
                     ?? configuration["Supabase:ServiceKey"]
                     ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
                     ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY")
                     ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY", EnvironmentVariableTarget.User)
                     ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY", EnvironmentVariableTarget.User)
                     ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY", EnvironmentVariableTarget.Machine)
                     ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY", EnvironmentVariableTarget.Machine);

        _storageServiceRoleKey = configuration["SUPABASE_STORAGE_SERVICE_ROLE_KEY"]
                              ?? configuration["Supabase:StorageServiceRoleKey"]
                              ?? Environment.GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY")
                              ?? Environment.GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY", EnvironmentVariableTarget.User)
                              ?? Environment.GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY", EnvironmentVariableTarget.Machine);

        if (_supabaseUrl != null)
        {
            _supabaseUrl = NormalizeSupabaseUrl(_supabaseUrl);
        }

        if (_serviceKey != null)
        {
            _serviceKey = _serviceKey.Trim().Trim('"', '\'');
        }

        if (_storageServiceRoleKey != null)
        {
            _storageServiceRoleKey = _storageServiceRoleKey.Trim().Trim('"', '\'');
        }

        if (IsLiveCloudConfigured)
        {
            var legacyConfigured = !string.IsNullOrWhiteSpace(EffectiveStorageJwt) ? "YES" : "NO";
            var modernConfigured = (!string.IsNullOrWhiteSpace(_serviceKey) && _serviceKey.StartsWith("sb_secret_", StringComparison.OrdinalIgnoreCase)) ? "YES" : "NO";

            _logger.LogInformation("SupabasePrivateStorageService initialized. Canonical Endpoint: {SupabaseUrl} | legacy storage key configured: {LegacyConfigured} | modern secret configured: {ModernConfigured}", 
                _supabaseUrl, legacyConfigured, modernConfigured);
        }
    }

    public static string NormalizeSupabaseUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

        var url = rawUrl.Trim().TrimEnd('/');

        // Collapse duplicate .supabase.co (e.g. .supabase.co.supabase.co)
        url = System.Text.RegularExpressions.Regex.Replace(
            url, 
            @"(\.supabase\.co)+$", 
            ".supabase.co", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Ensure http:// or https:// scheme
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        // If bare project reference (no dots in host)
        try
        {
            var uri = new Uri(url);
            if (!uri.Host.Contains('.'))
            {
                url = $"https://{uri.Host}.supabase.co";
            }
        }
        catch { }

        return url.TrimEnd('/');
    }

    private void EnsureConfigured()
    {
        if (!IsLiveCloudConfigured)
        {
            throw new InvalidOperationException(
                "Real Supabase Private Storage is not configured. Missing required environment variables: SUPABASE_URL and SUPABASE_SERVICE_KEY.");
        }

        if (string.IsNullOrWhiteSpace(EffectiveStorageJwt) && 
            !string.IsNullOrWhiteSpace(_serviceKey) && 
            _serviceKey.StartsWith("sb_secret_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Direct Supabase Storage REST operations require a legacy service_role JWT for Authorization header. " +
                "Missing required environment variable: SUPABASE_STORAGE_SERVICE_ROLE_KEY (must be an eyJ... service_role JWT). " +
                "Modern sb_secret key cannot be used as Bearer token.");
        }
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        var storageJwt = EffectiveStorageJwt;

        if (!string.IsNullOrWhiteSpace(storageJwt))
        {
            // Legacy service_role JWT provides required Authorization: Bearer <jwt> for Storage REST
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", storageJwt);
            request.Headers.TryAddWithoutValidation("apikey", storageJwt);
        }
        else if (!string.IsNullOrWhiteSpace(_serviceKey))
        {
            // Modern secret key (sb_secret_...) sent strictly via apikey header
            // DO NOT send as Authorization: Bearer, which triggers 'Invalid Compact JWS'
            request.Headers.TryAddWithoutValidation("apikey", _serviceKey);
        }
    }

    private string SanitizeSecret(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sanitized = input;
        if (!string.IsNullOrEmpty(_serviceKey))
        {
            sanitized = sanitized.Replace(_serviceKey, "[REDACTED_SECRET]");
        }
        if (!string.IsNullOrEmpty(_storageServiceRoleKey))
        {
            sanitized = sanitized.Replace(_storageServiceRoleKey, "[REDACTED_STORAGE_KEY]");
        }
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"eyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+", "[REDACTED_JWT]");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"sb_secret_[A-Za-z0-9-_]+", "[REDACTED_SECRET_KEY]");
        return sanitized;
    }

    public async Task EnsurePrivateBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var requestUri = $"{_supabaseUrl}/storage/v1/bucket";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        AddAuthHeaders(request);
        
        var payload = JsonSerializer.Serialize(new
        {
            id = bucketName,
            name = bucketName,
            @public = false
        });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Private bucket '{BucketName}' verified/created in Supabase Storage.", bucketName);
        }
        else
        {
            // If already exists, Supabase returns 400 with duplicate message, which is expected
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Bucket creation response: {StatusCode} - {Body}", response.StatusCode, SanitizeSecret(body));
        }
    }

    public async Task<string> UploadCertificateAsync(string bucketName, string objectPath, Stream fileStream, string contentType, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        // Target: POST /storage/v1/object/{bucket}/{objectPath}
        var cleanPath = objectPath.TrimStart('/');
        var requestUri = $"{_supabaseUrl}/storage/v1/object/{bucketName}/{cleanPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        AddAuthHeaders(request);
        request.Headers.Add("x-upsert", "true");

        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            var safeErr = SanitizeSecret(err);
            _logger.LogError("Supabase upload failed: {StatusCode} - {Error}", response.StatusCode, safeErr);
            throw new InvalidOperationException($"Supabase storage upload failed ({response.StatusCode}): {safeErr}");
        }

        _logger.LogInformation("Successfully uploaded private cloud object to {BucketName}/{Path}", bucketName, cleanPath);
        return $"{bucketName}/{cleanPath}";
    }

    public async Task<string> CreateSignedUrlAsync(string bucketName, string objectPath, int expiresInSeconds = 900, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        // Target: POST /storage/v1/object/sign/{bucket}/{objectPath}
        var cleanPath = objectPath.StartsWith($"{bucketName}/", StringComparison.OrdinalIgnoreCase) 
            ? objectPath.Substring(bucketName.Length + 1) 
            : objectPath.TrimStart('/');
        var requestUri = $"{_supabaseUrl}/storage/v1/object/sign/{bucketName}/{cleanPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        AddAuthHeaders(request);

        var payload = JsonSerializer.Serialize(new { expiresIn = expiresInSeconds });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            var safeErr = SanitizeSecret(err);
            _logger.LogError("Supabase sign URL failed: {StatusCode} - {Error}", response.StatusCode, safeErr);
            throw new InvalidOperationException($"Supabase sign URL generation failed ({response.StatusCode}): {safeErr}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("signedURL", out var signedUrlElement) ||
            doc.RootElement.TryGetProperty("signedUrl", out signedUrlElement))
        {
            var signedUrl = signedUrlElement.GetString();
            if (!string.IsNullOrWhiteSpace(signedUrl))
            {
                // Returns full signed URL with project domain
                if (signedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    signedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return signedUrl;
                }

                // If relative path does not include /storage/v1, prepend it
                // Supabase Storage returns relative path such as "/object/sign/certificates/..."
                // When accessed externally via Supabase Kong gateway, the public route is "/storage/v1/object/sign/..."
                var relativePath = signedUrl.StartsWith("/") ? signedUrl : "/" + signedUrl;
                if (!relativePath.StartsWith("/storage/v1/", StringComparison.OrdinalIgnoreCase) &&
                    !relativePath.Equals("/storage/v1", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = $"/storage/v1{relativePath}";
                }

                return $"{_supabaseUrl}{relativePath}";
            }
        }

        throw new InvalidOperationException("Invalid signed URL response from Supabase Storage.");
    }

    public async Task<bool> DeleteCertificateAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var cleanPath = objectPath.StartsWith($"{bucketName}/", StringComparison.OrdinalIgnoreCase) 
            ? objectPath.Substring(bucketName.Length + 1) 
            : objectPath.TrimStart('/');
        var requestUri = $"{_supabaseUrl}/storage/v1/object/{bucketName}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        AddAuthHeaders(request);

        var payload = JsonSerializer.Serialize(new { prefixes = new[] { cleanPath } });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ObjectExistsAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var cleanPath = objectPath.StartsWith($"{bucketName}/", StringComparison.OrdinalIgnoreCase) 
            ? objectPath.Substring(bucketName.Length + 1) 
            : objectPath.TrimStart('/');
        var folder = Path.GetDirectoryName(cleanPath)?.Replace('\\', '/') ?? "";
        var fileName = Path.GetFileName(cleanPath);

        var requestUri = $"{_supabaseUrl}/storage/v1/object/list/{bucketName}";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        AddAuthHeaders(request);

        var payload = JsonSerializer.Serialize(new
        {
            prefix = folder,
            limit = 100,
            search = fileName
        });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var nameProp) && nameProp.GetString() == fileName)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
