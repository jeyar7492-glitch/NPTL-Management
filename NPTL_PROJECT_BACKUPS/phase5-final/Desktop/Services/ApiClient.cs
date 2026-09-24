using System.IO;
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

    #region Phase 4 — Admin API Methods
    public async Task<AdminDashboardMetricsDto> GetAdminDashboardMetricsAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminDashboardMetricsDto>(
            HttpMethod.Get,
            "api/v1/admin/dashboard",
            null,
            includeAuth: true,
            cancellationToken);
    }

    // Admin Students
    public async Task<PagedResult<AdminStudentListDto>> GetAdminStudentsAsync(
        string? search, string? department, int? year, string? classSection, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder($"api/v1/admin/students?page={page}&pageSize={pageSize}");
        if (!string.IsNullOrWhiteSpace(search)) sb.Append($"&search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(department)) sb.Append($"&department={Uri.EscapeDataString(department)}");
        if (year.HasValue && year.Value > 0) sb.Append($"&year={year.Value}");
        if (!string.IsNullOrWhiteSpace(classSection)) sb.Append($"&classSection={Uri.EscapeDataString(classSection)}");
        if (isActive.HasValue) sb.Append($"&isActive={isActive.Value}");

        return await SendAsync<PagedResult<AdminStudentListDto>>(HttpMethod.Get, sb.ToString(), null, true, cancellationToken);
    }

    public async Task<AdminStudentDetailDto> GetAdminStudentByIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminStudentDetailDto>(HttpMethod.Get, $"api/v1/admin/students/{studentId}", null, true, cancellationToken);
    }

    public async Task<AdminStudentDetailDto> CreateAdminStudentAsync(CreateStudentDto dto, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminStudentDetailDto>(HttpMethod.Post, "api/v1/admin/students", dto, true, cancellationToken);
    }

    public async Task<AdminStudentDetailDto> UpdateAdminStudentAsync(Guid studentId, UpdateStudentDto dto, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminStudentDetailDto>(HttpMethod.Put, $"api/v1/admin/students/{studentId}", dto, true, cancellationToken);
    }

    public async Task<bool> UpdateAdminStudentStatusAsync(Guid studentId, bool isActive, CancellationToken cancellationToken = default)
    {
        await SendAsync<object>(HttpMethod.Patch, $"api/v1/admin/students/{studentId}/status", new { isActive }, true, cancellationToken);
        return true;
    }

    // Admin Staff
    public async Task<PagedResult<AdminStaffListDto>> GetAdminStaffAsync(
        string? search, string? department, int? assignedYear, string? assignedClass, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder($"api/v1/admin/staff?page={page}&pageSize={pageSize}");
        if (!string.IsNullOrWhiteSpace(search)) sb.Append($"&search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(department)) sb.Append($"&department={Uri.EscapeDataString(department)}");
        if (assignedYear.HasValue && assignedYear.Value > 0) sb.Append($"&assignedYear={assignedYear.Value}");
        if (!string.IsNullOrWhiteSpace(assignedClass)) sb.Append($"&assignedClass={Uri.EscapeDataString(assignedClass)}");
        if (isActive.HasValue) sb.Append($"&isActive={isActive.Value}");

        return await SendAsync<PagedResult<AdminStaffListDto>>(HttpMethod.Get, sb.ToString(), null, true, cancellationToken);
    }

    public async Task<AdminStaffDetailDto> GetAdminStaffByIdAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminStaffDetailDto>(HttpMethod.Get, $"api/v1/admin/staff/{staffId}", null, true, cancellationToken);
    }

    public async Task<AdminStaffDetailDto> CreateAdminStaffAsync(CreateStaffDto dto, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminStaffDetailDto>(HttpMethod.Post, "api/v1/admin/staff", dto, true, cancellationToken);
    }

    public async Task<AdminStaffDetailDto> UpdateAdminStaffAsync(Guid staffId, UpdateStaffDto dto, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminStaffDetailDto>(HttpMethod.Put, $"api/v1/admin/staff/{staffId}", dto, true, cancellationToken);
    }

    public async Task<bool> UpdateAdminStaffStatusAsync(Guid staffId, bool isActive, CancellationToken cancellationToken = default)
    {
        await SendAsync<object>(HttpMethod.Patch, $"api/v1/admin/staff/{staffId}/status", new { isActive }, true, cancellationToken);
        return true;
    }

    public async Task<bool> ResetAdminStaffPasswordAsync(Guid staffId, string newPassword, CancellationToken cancellationToken = default)
    {
        await SendAsync<object>(HttpMethod.Post, $"api/v1/admin/staff/{staffId}/reset-password", new { newPassword }, true, cancellationToken);
        return true;
    }

    // Admin Courses
    public async Task<PagedResult<AdminCourseDto>> GetAdminCoursesAsync(
        string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder($"api/v1/admin/courses?page={page}&pageSize={pageSize}");
        if (!string.IsNullOrWhiteSpace(search)) sb.Append($"&search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(status)) sb.Append($"&status={Uri.EscapeDataString(status)}");

        return await SendAsync<PagedResult<AdminCourseDto>>(HttpMethod.Get, sb.ToString(), null, true, cancellationToken);
    }

    public async Task<AdminCourseDto> GetAdminCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminCourseDto>(HttpMethod.Get, $"api/v1/admin/courses/{courseId}", null, true, cancellationToken);
    }

    public async Task<AdminCourseDto> CreateAdminCourseAsync(CreateCourseDto dto, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminCourseDto>(HttpMethod.Post, "api/v1/admin/courses", dto, true, cancellationToken);
    }

    public async Task<AdminCourseDto> UpdateAdminCourseAsync(Guid courseId, UpdateCourseDto dto, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminCourseDto>(HttpMethod.Put, $"api/v1/admin/courses/{courseId}", dto, true, cancellationToken);
    }

    public async Task<bool> UpdateAdminCourseStatusAsync(Guid courseId, string status, CancellationToken cancellationToken = default)
    {
        await SendAsync<object>(HttpMethod.Patch, $"api/v1/admin/courses/{courseId}/status", new { status }, true, cancellationToken);
        return true;
    }

    // Admin Registrations
    public async Task<PagedResult<AdminRegistrationDto>> GetAdminRegistrationsAsync(
        string? search, string? status, Guid? studentId, Guid? courseId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder($"api/v1/admin/registrations?page={page}&pageSize={pageSize}");
        if (!string.IsNullOrWhiteSpace(search)) sb.Append($"&search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(status)) sb.Append($"&status={Uri.EscapeDataString(status)}");
        if (studentId.HasValue && studentId.Value != Guid.Empty) sb.Append($"&studentId={studentId.Value}");
        if (courseId.HasValue && courseId.Value != Guid.Empty) sb.Append($"&courseId={courseId.Value}");

        return await SendAsync<PagedResult<AdminRegistrationDto>>(HttpMethod.Get, sb.ToString(), null, true, cancellationToken);
    }

    public async Task<AdminRegistrationDto> GetAdminRegistrationByIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminRegistrationDto>(HttpMethod.Get, $"api/v1/admin/registrations/{registrationId}", null, true, cancellationToken);
    }

    public async Task<AdminRegistrationDto> CreateAdminRegistrationAsync(CreateRegistrationDto dto, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminRegistrationDto>(HttpMethod.Post, "api/v1/admin/registrations", dto, true, cancellationToken);
    }

    public async Task<AdminRegistrationDto> UpdateAdminRegistrationStatusAsync(Guid registrationId, string status, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminRegistrationDto>(HttpMethod.Patch, $"api/v1/admin/registrations/{registrationId}/status", new { status }, true, cancellationToken);
    }

    // Admin Exams
    public async Task<PagedResult<AdminExamDto>> GetAdminExamsAsync(
        string? search, string? examStatus, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder($"api/v1/admin/exams?page={page}&pageSize={pageSize}");
        if (!string.IsNullOrWhiteSpace(search)) sb.Append($"&search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(examStatus)) sb.Append($"&examStatus={Uri.EscapeDataString(examStatus)}");

        return await SendAsync<PagedResult<AdminExamDto>>(HttpMethod.Get, sb.ToString(), null, true, cancellationToken);
    }

    public async Task<AdminExamDto> GetAdminExamByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminExamDto>(HttpMethod.Get, $"api/v1/admin/exams/registration/{registrationId}", null, true, cancellationToken);
    }

    public async Task<AdminExamDto> UpdateAdminExamAsync(Guid registrationId, UpdateAdminExamDto dto, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminExamDto>(HttpMethod.Put, $"api/v1/admin/exams/registration/{registrationId}", dto, true, cancellationToken);
    }

    // Admin Certificates & Cloud Access
    public async Task<PagedResult<AdminCertificateDto>> GetAdminCertificatesAsync(
        string? search, string? verifiedStatus, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder($"api/v1/admin/certificates?page={page}&pageSize={pageSize}");
        if (!string.IsNullOrWhiteSpace(search)) sb.Append($"&search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(verifiedStatus)) sb.Append($"&verifiedStatus={Uri.EscapeDataString(verifiedStatus)}");

        return await SendAsync<PagedResult<AdminCertificateDto>>(HttpMethod.Get, sb.ToString(), null, true, cancellationToken);
    }

    public async Task<AdminCertificateDto> GetAdminCertificateByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminCertificateDto>(HttpMethod.Get, $"api/v1/admin/certificates/registration/{registrationId}", null, true, cancellationToken);
    }

    public async Task<AdminCertificateDto> UploadAdminCertificateAsync(Guid registrationId, string filePath, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var requestUri = $"api/v1/admin/certificates/registration/{registrationId}/upload";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = form
        };

        if (!string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = TryDeserializeApiResponse<object>(rawContent);
            throw new ApiException(err?.Message ?? "Failed to upload certificate.", response.StatusCode);
        }

        var apiResp = TryDeserializeApiResponse<AdminCertificateDto>(rawContent);
        return apiResp?.Data ?? throw new ApiException("Invalid server response after certificate upload.");
    }

    public async Task<AdminCertificateDto> UpdateAdminCertificateStatusAsync(
        Guid registrationId, string verifiedStatus, DateTime? verifiedDate = null, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminCertificateDto>(
            HttpMethod.Patch,
            $"api/v1/admin/certificates/registration/{registrationId}/status",
            new { verifiedStatus, verifiedDate },
            true,
            cancellationToken);
    }

    public async Task<CertificateAccessResponseDto> GetCertificateAccessAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        string roleSegment = (_session.UserRole?.ToLowerInvariant()) switch
        {
            "student" => "student",
            "staff" => "staff",
            _ => "admin"
        };

        return await SendAsync<CertificateAccessResponseDto>(
            HttpMethod.Get,
            $"api/v1/{roleSegment}/certificates/{certificateId}/access",
            null,
            true,
            cancellationToken);
    }

    // Admin Notifications
    public async Task<PagedResult<AdminNotificationDto>> GetAdminNotificationsAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder($"api/v1/admin/notifications?page={page}&pageSize={pageSize}");
        if (!string.IsNullOrWhiteSpace(search)) sb.Append($"&search={Uri.EscapeDataString(search)}");

        return await SendAsync<PagedResult<AdminNotificationDto>>(HttpMethod.Get, sb.ToString(), null, true, cancellationToken);
    }

    public async Task<int> CreateAdminNotificationAsync(CreateNotificationDto dto, CancellationToken cancellationToken = default)
    {
        var resp = await SendAsync<JsonElement>(HttpMethod.Post, "api/v1/admin/notifications", dto, true, cancellationToken);
        if (resp.TryGetProperty("dispatchedCount", out var countElem))
        {
            return countElem.GetInt32();
        }
        return 1;
    }

    public async Task<bool> DeleteAdminNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        await SendAsync<object>(HttpMethod.Delete, $"api/v1/admin/notifications/{notificationId}", null, true, cancellationToken);
        return true;
    }

    public async Task<int> TriggerAdminNotificationRulesAsync(CancellationToken cancellationToken = default)
    {
        var resp = await SendAsync<JsonElement>(HttpMethod.Post, "api/v1/admin/notifications/trigger-rules", new { }, true, cancellationToken);
        if (resp.TryGetProperty("generatedCount", out var countElem))
        {
            return countElem.GetInt32();
        }
        return 0;
    }

    // Admin Reports & Exports
    public async Task<AdminReportPreviewDto> GetAdminReportPreviewAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        return await SendAsync<AdminReportPreviewDto>(HttpMethod.Post, "api/v1/admin/reports/preview", filter, true, cancellationToken);
    }

    public async Task<byte[]> ExportAdminReportPdfAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        return await DownloadReportBytesAsync("api/v1/admin/reports/export-pdf", filter, cancellationToken);
    }

    public async Task<byte[]> ExportAdminReportXlsxAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        return await DownloadReportBytesAsync("api/v1/admin/reports/export-xlsx", filter, cancellationToken);
    }

    public async Task<byte[]> ExportAdminReportCsvAsync(AdminReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        return await DownloadReportBytesAsync("api/v1/admin/reports/export-csv", filter, cancellationToken);
    }

    private async Task<byte[]> DownloadReportBytesAsync(string endpoint, AdminReportFilterDto filter, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(filter, _jsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.TrimStart('/'))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var err = TryDeserializeApiResponse<object>(raw);
            throw new ApiException(err?.Message ?? $"Export failed with status code {(int)response.StatusCode}", response.StatusCode);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    // Admin Audit Logs
    public async Task<PagedResult<AuditLogDto>> GetAdminAuditLogsAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder($"api/v1/admin/audit-logs?page={filter.Page}&pageSize={filter.PageSize}");
        if (!string.IsNullOrWhiteSpace(filter.Search)) sb.Append($"&search={Uri.EscapeDataString(filter.Search)}");
        if (!string.IsNullOrWhiteSpace(filter.Action)) sb.Append($"&action={Uri.EscapeDataString(filter.Action)}");
        if (!string.IsNullOrWhiteSpace(filter.Role)) sb.Append($"&role={Uri.EscapeDataString(filter.Role)}");
        if (filter.DateFrom.HasValue) sb.Append($"&dateFrom={filter.DateFrom.Value:yyyy-MM-dd}");
        if (filter.DateTo.HasValue) sb.Append($"&dateTo={filter.DateTo.Value:yyyy-MM-dd}");

        return await SendAsync<PagedResult<AuditLogDto>>(HttpMethod.Get, sb.ToString(), null, true, cancellationToken);
    }
    #endregion

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
