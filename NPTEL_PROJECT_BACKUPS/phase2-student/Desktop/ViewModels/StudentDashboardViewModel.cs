using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StudentDashboardViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService? _navigationService;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private StudentProfileResponse? _profile;
    private StudentDashboardSummaryDto? _summary;

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

    public StudentProfileResponse? Profile
    {
        get => _profile;
        set
        {
            if (SetProperty(ref _profile, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DisplayRegisterNumber));
                OnPropertyChanged(nameof(DisplayDepartment));
                OnPropertyChanged(nameof(DisplayClassSection));
                OnPropertyChanged(nameof(DisplayYear));
                OnPropertyChanged(nameof(DisplayBatch));
                OnPropertyChanged(nameof(DisplayEmail));
                OnPropertyChanged(nameof(DisplayPhone));
            }
        }
    }

    public StudentDashboardSummaryDto? Summary
    {
        get => _summary;
        set
        {
            if (SetProperty(ref _summary, value))
            {
                OnPropertyChanged(nameof(RegisteredCoursesCount));
                OnPropertyChanged(nameof(InProgressCoursesCount));
                OnPropertyChanged(nameof(CompletedCoursesCount));
                OnPropertyChanged(nameof(ExamPendingCount));
                OnPropertyChanged(nameof(CertificatePendingCount));
                OnPropertyChanged(nameof(CertificateVerifiedCount));
            }
        }
    }

    public string DisplayName => !string.IsNullOrWhiteSpace(Profile?.Name) ? Profile.Name : "—";
    public string DisplayRegisterNumber => !string.IsNullOrWhiteSpace(Profile?.RegisterNumber) ? Profile.RegisterNumber : "—";
    public string DisplayDepartment => !string.IsNullOrWhiteSpace(Profile?.Department) ? Profile.Department : "—";
    public string DisplayClassSection => !string.IsNullOrWhiteSpace(Profile?.ClassSection) ? Profile.ClassSection : "—";
    public string DisplayYear => Profile?.Year > 0 ? $"Year {Profile.Year}" : "—";
    public string DisplayBatch => !string.IsNullOrWhiteSpace(Profile?.Batch) ? Profile.Batch : "—";
    public string DisplayEmail => !string.IsNullOrWhiteSpace(Profile?.Email) ? Profile.Email : "Not provided";
    public string DisplayPhone => !string.IsNullOrWhiteSpace(Profile?.Phone) ? Profile.Phone : "Not provided";

    public int RegisteredCoursesCount => Summary?.RegisteredCourses ?? 0;
    public int InProgressCoursesCount => Summary?.InProgressCourses ?? 0;
    public int CompletedCoursesCount => Summary?.CompletedCourses ?? 0;
    public int ExamPendingCount => Summary?.ExamPending ?? 0;
    public int CertificatePendingCount => Summary?.CertificatesPending ?? 0;
    public int CertificateVerifiedCount => Summary?.CertificatesVerified ?? 0;

    public ICommand RefreshCommand { get; }
    public ICommand NavigateToCoursesCommand { get; }
    public ICommand NavigateToNotificationsCommand { get; }

    public StudentDashboardViewModel(IApiClient apiClient, INavigationService? navigationService = null)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;

        RefreshCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
        NavigateToCoursesCommand = new RelayCommand(() => _navigationService?.NavigateShellTo<StudentCoursesViewModel>());
        NavigateToNotificationsCommand = new RelayCommand(() => _navigationService?.NavigateShellTo<StudentNotificationsViewModel>());
    }

    public async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var profileTask = _apiClient.GetStudentProfileAsync();
            var summaryTask = _apiClient.GetStudentDashboardSummaryAsync();

            await Task.WhenAll(profileTask, summaryTask);

            Profile = await profileTask;
            Summary = await summaryTask;
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Unable to load student dashboard details.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
