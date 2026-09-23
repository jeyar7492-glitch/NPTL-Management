using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NPTELManagement.Core.Common;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Models;

namespace NPTELManagement.Desktop.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationSession _session;
    private readonly ApiSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(IAuthenticationSession? session = null, ApiSettings? settings = null, HttpClient? httpClient = null)
    {
        _session = session ?? AuthenticationSession.Instance;
        _settings = settings ?? ApiSettings.Instance;

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = _settings.Timeout;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<HealthCheckResult> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(ApiSettings.HealthEndpoint.TrimStart('/'), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new HealthCheckResult { Status = "unavailable", Database = "disconnected" };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<HealthCheckResult>(content, _jsonOptions);
            return result ?? new HealthCheckResult { Status = "unknown", Database = "unknown" };
        }
        catch (HttpRequestException)
        {
            return new HealthCheckResult { Status = "offline", Database = "disconnected" };
        }
        catch (TaskCanceledException)
        {
            return new HealthCheckResult { Status = "timeout", Database = "disconnected" };
        }
    }

    public async Task<AuthSuccessResponse> LoginStudentAsync(StudentLoginDto dto, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<AuthSuccessResponse>(
            HttpMethod.Post, 
            ApiSettings.StudentLoginEndpoint, 
            dto, 
            includeAuth: false, 
            cancellationToken);

        _session.SetSession(response);
        return response;
    }

    public async Task<AuthSuccessResponse> LoginStaffAsync(StaffLoginDto dto, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<AuthSuccessResponse>(
            HttpMethod.Post, 
            ApiSettings.StaffLoginEndpoint, 
            dto, 
            includeAuth: false, 
            cancellationToken);

        _session.SetSession(response);
        return response;
    }

    public async Task<AuthSuccessResponse> LoginAdminAsync(AdminLoginDto dto, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<AuthSuccessResponse>(
            HttpMethod.Post, 
            ApiSettings.AdminLoginEndpoint, 
            dto, 
            includeAuth: false, 
            cancellationToken);

        _session.SetSession(response);
        return response;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_session.IsAuthenticated)
            {
                await SendAsync<object>(HttpMethod.Post, ApiSettings.LogoutEndpoint, null, includeAuth: true, cancellationToken);
            }
        }
        catch
        {
            // Even if network call fails, we always clear the local session
        }
        finally
        {
            _session.ClearSession();
        }
    }

    public async Task<StudentProfileResponse> GetStudentProfileAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<StudentProfileResponse>(
            HttpMethod.Get, 
            ApiSettings.StudentMeEndpoint, 
            null, 
            includeAuth: true, 
            cancellationToken);
    }

    public async Task<StudentDashboardSummaryDto> GetStudentDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<StudentDashboardSummaryDto>(
            HttpMethod.Get,
            ApiSettings.StudentDashboardSummaryEndpoint,
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<List<StudentCourseDto>> GetStudentCoursesAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<List<StudentCourseDto>>(
            HttpMethod.Get,
            ApiSettings.StudentCoursesEndpoint,
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<StudentCourseDetailsDto> GetStudentCourseDetailsAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<StudentCourseDetailsDto>(
            HttpMethod.Get,
            $"{ApiSettings.StudentCoursesEndpoint}/{registrationId}",
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<List<StudentNotificationDto>> GetStudentNotificationsAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<List<StudentNotificationDto>>(
            HttpMethod.Get,
            ApiSettings.StudentNotificationsEndpoint,
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<bool> MarkNotificationAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        await SendAsync<object>(
            HttpMethod.Post,
            $"{ApiSettings.StudentNotificationsEndpoint}/{notificationId}/read",
            null,
            includeAuth: true,
            cancellationToken);
        return true;
    }

    public async Task<StaffProfileResponse> GetStaffProfileAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<StaffProfileResponse>(
            HttpMethod.Get, 
            ApiSettings.StaffMeEndpoint, 
            null, 
            includeAuth: true, 
            cancellationToken);
    }

    public async Task<StaffDashboardSummaryDto> GetStaffDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<StaffDashboardSummaryDto>(
            HttpMethod.Get,
            ApiSettings.StaffDashboardSummaryEndpoint,
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<PagedResult<StaffStudentListDto>> GetStaffStudentsAsync(StaffStudentFilterDto filter, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Search))
            queryParams.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        if (filter.Year.HasValue)
            queryParams.Add($"year={filter.Year.Value}");
        if (!string.IsNullOrWhiteSpace(filter.ClassSection) && filter.ClassSection != "All")
            queryParams.Add($"classSection={Uri.EscapeDataString(filter.ClassSection.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.Course) && filter.Course != "All")
            queryParams.Add($"course={Uri.EscapeDataString(filter.Course.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.RegistrationStatus) && filter.RegistrationStatus != "All")
            queryParams.Add($"registrationStatus={Uri.EscapeDataString(filter.RegistrationStatus.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.ExamStatus) && filter.ExamStatus != "All")
            queryParams.Add($"examStatus={Uri.EscapeDataString(filter.ExamStatus.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.CertificateStatus) && filter.CertificateStatus != "All")
            queryParams.Add($"certificateStatus={Uri.EscapeDataString(filter.CertificateStatus.Trim())}");
        if (filter.Page > 1)
            queryParams.Add($"page={filter.Page}");
        if (filter.PageSize != 25)
            queryParams.Add($"pageSize={filter.PageSize}");

        var url = ApiSettings.StaffStudentsEndpoint;
        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        var result = await SendAsync<PagedResult<StaffStudentListDto>>(
            HttpMethod.Get,
            url,
            null,
            includeAuth: true,
            cancellationToken);

        return result ?? new PagedResult<StaffStudentListDto>();
    }

    public async Task<List<StudentProfileResponse>> GetAssignedStudentsAsync(
        string? search = null, 
        string? classSection = null, 
        CancellationToken cancellationToken = default)
    {
        var filter = new StaffStudentFilterDto
        {
            Search = search,
            ClassSection = classSection,
            PageSize = 100
        };
        var paged = await GetStaffStudentsAsync(filter, cancellationToken);
        return paged.Items.Select(s => new StudentProfileResponse
        {
            StudentId = s.StudentId,
            Name = s.Name,
            RegisterNumber = s.RegisterNumber,
            Department = s.Department,
            ClassSection = s.ClassSection,
            Year = s.Year,
            Batch = s.Batch,
            Email = s.Email,
            Phone = s.Phone
        }).ToList();
    }

    public async Task<StaffStudentDetailsDto> GetStaffStudentDetailsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiSettings.StaffStudentsEndpoint}/{studentId}";
        return await SendAsync<StaffStudentDetailsDto>(
            HttpMethod.Get,
            url,
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<List<StaffStudentCourseDto>> GetStaffStudentCoursesAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiSettings.StaffStudentsEndpoint}/{studentId}/courses";
        var result = await SendAsync<List<StaffStudentCourseDto>>(
            HttpMethod.Get,
            url,
            null,
            includeAuth: true,
            cancellationToken);

        return result ?? new List<StaffStudentCourseDto>();
    }

    public async Task<List<StudentTimelineItemDto>> GetStaffStudentTimelineAsync(Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiSettings.StaffStudentsEndpoint}/{studentId}/timeline";
        if (registrationId.HasValue)
        {
            url += $"?registrationId={registrationId.Value}";
        }
        var result = await SendAsync<List<StudentTimelineItemDto>>(
            HttpMethod.Get,
            url,
            null,
            includeAuth: true,
            cancellationToken);

        return result ?? new List<StudentTimelineItemDto>();
    }

    public async Task<StudentExamDto> GetStaffStudentExamAsync(Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiSettings.StaffStudentsEndpoint}/{studentId}/exam";
        if (registrationId.HasValue)
        {
            url += $"?registrationId={registrationId.Value}";
        }
        return await SendAsync<StudentExamDto>(
            HttpMethod.Get,
            url,
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<StudentCertificateDto> GetStaffStudentCertificateAsync(Guid studentId, Guid? registrationId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiSettings.StaffStudentsEndpoint}/{studentId}/certificate";
        if (registrationId.HasValue)
        {
            url += $"?registrationId={registrationId.Value}";
        }
        return await SendAsync<StudentCertificateDto>(
            HttpMethod.Get,
            url,
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<StaffReportPreviewDto> GetStaffReportPreviewAsync(string reportType, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiSettings.StaffReportsPreviewEndpoint}?reportType={Uri.EscapeDataString(reportType)}";
        return await SendAsync<StaffReportPreviewDto>(
            HttpMethod.Get,
            url,
            null,
            includeAuth: true,
            cancellationToken);
    }

    public async Task<StudentProfileResponse> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiSettings.StaffStudentsEndpoint}/{id}";
        return await SendAsync<StudentProfileResponse>(
            HttpMethod.Get, 
            url, 
            null, 
            includeAuth: true, 
            cancellationToken);
    }

    public async Task<AdminProfileResponse> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminProfileResponse>(
            HttpMethod.Get, 
            ApiSettings.AdminMeEndpoint, 
            null, 
            includeAuth: true, 
            cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method, 
        string endpoint, 
        object? body, 
        bool includeAuth, 
        CancellationToken cancellationToken)
    {
        var requestUri = endpoint.TrimStart('/');
        using var request = new HttpRequestMessage(method, requestUri);

        if (includeAuth && !string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkUnavailableException($"Unable to connect to the NPTEL Management server. Please ensure the backend is running. Details: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NetworkUnavailableException("The request to the server timed out. Please try again.");
        }

        using (response)
        {
            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Handle status codes
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _session.ClearSession();
                var errObj = TryDeserializeApiResponse<object>(rawContent);
                var message = !string.IsNullOrWhiteSpace(errObj?.Message) ? errObj.Message : "Invalid credentials or session expired.";
                throw new UnauthorizedException(message);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var errObj = TryDeserializeApiResponse<object>(rawContent);
                var message = !string.IsNullOrWhiteSpace(errObj?.Message) ? errObj.Message : "Access denied. You do not have permission to view this resource.";
                throw new AccessDeniedException(message);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var errObj = TryDeserializeApiResponse<object>(rawContent);
                var message = !string.IsNullOrWhiteSpace(errObj?.Message) ? errObj.Message : "Resource not found.";
                throw new NotFoundException(message);
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                var errObj = TryDeserializeApiResponse<object>(rawContent);
                var message = !string.IsNullOrWhiteSpace(errObj?.Message) ? errObj.Message : "Too many requests. Please wait before trying again.";
                throw new ApiException(message, (HttpStatusCode)429);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errObj = TryDeserializeApiResponse<object>(rawContent);
                var message = !string.IsNullOrWhiteSpace(errObj?.Message) ? errObj.Message : "Invalid request data.";
                throw new ApiException(message, HttpStatusCode.BadRequest, errObj?.Errors);
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new ServerErrorException("An internal server error occurred. Please contact the administrator.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException($"Server returned status code {(int)response.StatusCode}", response.StatusCode);
            }

            // Success (200 OK)
            var apiResponse = TryDeserializeApiResponse<T>(rawContent);
            if (apiResponse == null)
            {
                throw new ApiException("Received an empty or invalid response envelope from the server.");
            }

            if (!apiResponse.Success)
            {
                throw new ApiException(apiResponse.Message ?? "Operation failed.", response.StatusCode, apiResponse.Errors);
            }

            if (apiResponse.Data == null && typeof(T) != typeof(object))
            {
                // Return default if value type or empty collection
                return default!;
            }

            return apiResponse.Data!;
        }
    }

    private ApiResponse<TData>? TryDeserializeApiResponse<TData>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<ApiResponse<TData>>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
