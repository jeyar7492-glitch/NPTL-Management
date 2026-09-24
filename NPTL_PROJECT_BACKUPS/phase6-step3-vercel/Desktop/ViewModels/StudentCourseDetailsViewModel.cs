using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StudentCourseDetailsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;
    private readonly Guid _registrationId;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private StudentCourseDetailsDto? _details;

    public Guid RegistrationId => _registrationId;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public StudentCourseDetailsDto? Details
    {
        get => _details;
        set
        {
            if (SetProperty(ref _details, value))
            {
                OnPropertyChanged(nameof(CourseName));
                OnPropertyChanged(nameof(CourseCode));
                OnPropertyChanged(nameof(DurationWeeksText));
                OnPropertyChanged(nameof(RegistrationStatus));
                OnPropertyChanged(nameof(CourseStartDateText));
                OnPropertyChanged(nameof(CourseEndDateText));
                OnPropertyChanged(nameof(EnrollmentDateText));
                OnPropertyChanged(nameof(TimelineItems));
                OnPropertyChanged(nameof(Exam));
                OnPropertyChanged(nameof(Certificate));

                // Exam displays
                OnPropertyChanged(nameof(ExamApplicationStatusDisplay));
                OnPropertyChanged(nameof(ExamApplicationDateDisplay));
                OnPropertyChanged(nameof(ExamApplicationDeadlineDisplay));
                OnPropertyChanged(nameof(ExamDateDisplay));
                OnPropertyChanged(nameof(HallTicketDisplay));
                OnPropertyChanged(nameof(ExamStatusDisplay));
                OnPropertyChanged(nameof(ScoreDisplay));
                OnPropertyChanged(nameof(PassStatusDisplay));

                // Certificate displays
                OnPropertyChanged(nameof(CertificateStatusDisplay));
                OnPropertyChanged(nameof(SubmittedDateDisplay));
                OnPropertyChanged(nameof(VerifiedDateDisplay));
                OnPropertyChanged(nameof(ReceivedDateDisplay));
            }
        }
    }

    public string CourseName => Details?.CourseName ?? "—";
    public string CourseCode => Details?.CourseCode ?? "—";
    public string DurationWeeksText => Details != null ? $"{Details.DurationWeeks} Weeks" : "—";
    public string RegistrationStatus => Details?.RegistrationStatus ?? "—";

    public string CourseStartDateText => Details?.CourseStartDate.HasValue == true 
        ? Details.CourseStartDate.Value.ToString("dd MMMM yyyy") 
        : "Not Scheduled";

    public string CourseEndDateText => Details?.CourseEndDate.HasValue == true 
        ? Details.CourseEndDate.Value.ToString("dd MMMM yyyy") 
        : "Not Scheduled";

    public string EnrollmentDateText => Details != null 
        ? Details.EnrollmentDate.ToString("dd MMMM yyyy") 
        : "—";

    public List<StudentTimelineItemDto> TimelineItems => Details?.Timeline ?? new List<StudentTimelineItemDto>();
    public StudentExamDto? Exam => Details?.Exam;
    public StudentCertificateDto? Certificate => Details?.Certificate;

    // Null-safe Exam Display Helpers
    public string ExamApplicationStatusDisplay => !string.IsNullOrWhiteSpace(Exam?.ExamApplicationStatus) ? Exam.ExamApplicationStatus : "Not Available";
    public string ExamApplicationDateDisplay => Exam?.ExamApplicationDate.HasValue == true ? Exam.ExamApplicationDate.Value.ToString("dd MMM yyyy") : "Not Available";
    public string ExamApplicationDeadlineDisplay => Exam?.ExamApplicationDeadline.HasValue == true ? Exam.ExamApplicationDeadline.Value.ToString("dd MMM yyyy") : "Not Available";
    public string ExamDateDisplay => Exam?.ExamDate.HasValue == true ? Exam.ExamDate.Value.ToString("dd MMM yyyy") : "Not Scheduled";
    public string HallTicketDisplay => !string.IsNullOrWhiteSpace(Exam?.HallTicketStatus) ? Exam.HallTicketStatus : "Not Available";
    public string ExamStatusDisplay => !string.IsNullOrWhiteSpace(Exam?.ExamStatus) ? Exam.ExamStatus : "Not Scheduled";
    public string ScoreDisplay => Exam?.Score.HasValue == true ? $"{Exam.Score.Value:F1} / 100" : "Not Available";
    public string PassStatusDisplay => !string.IsNullOrWhiteSpace(Exam?.PassStatus) ? Exam.PassStatus : "Not Available";

    // Null-safe Certificate Display Helpers
    public string CertificateStatusDisplay => !string.IsNullOrWhiteSpace(Certificate?.VerifiedStatus) ? Certificate.VerifiedStatus : "Pending";
    public string SubmittedDateDisplay => Certificate?.SubmittedDate.HasValue == true ? Certificate.SubmittedDate.Value.ToString("dd MMM yyyy") : "Not Available";
    public string VerifiedDateDisplay => Certificate?.VerifiedDate.HasValue == true ? Certificate.VerifiedDate.Value.ToString("dd MMM yyyy") : "Not Available";
    public string ReceivedDateDisplay => Certificate?.ReceivedDate.HasValue == true ? Certificate.ReceivedDate.Value.ToString("dd MMM yyyy") : "Not Available";

    public ICommand BackCommand { get; }
    public ICommand RefreshCommand { get; }

    public StudentCourseDetailsViewModel(IApiClient apiClient, INavigationService navigationService, Guid registrationId)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;
        _registrationId = registrationId;

        BackCommand = new RelayCommand(() => _navigationService.NavigateShellTo<StudentCoursesViewModel>());
        RefreshCommand = new AsyncRelayCommand(LoadDetailsAsync);
    }

    public async Task LoadDetailsAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var data = await _apiClient.GetStudentCourseDetailsAsync(_registrationId);
            Details = data;
        }
        catch (NotFoundException)
        {
            HasError = true;
            ErrorMessage = "Course registration details could not be found or you do not have permission to view them.";
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Unable to load course details. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
