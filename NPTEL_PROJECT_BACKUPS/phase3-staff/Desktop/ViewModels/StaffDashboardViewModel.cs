using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StaffDashboardViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;

    private StaffProfileResponse? _staffProfile;
    private StaffDashboardSummaryDto _summary = new();

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

    public StaffProfileResponse? StaffProfile
    {
        get => _staffProfile;
        set
        {
            if (SetProperty(ref _staffProfile, value))
            {
                OnPropertyChanged(nameof(StaffDisplayName));
                OnPropertyChanged(nameof(StaffDisplayId));
                OnPropertyChanged(nameof(StaffDisplayDepartment));
                OnPropertyChanged(nameof(StaffDisplayAssignedScope));
            }
        }
    }

    public StaffDashboardSummaryDto Summary
    {
        get => _summary;
        set
        {
            if (SetProperty(ref _summary, value))
            {
                OnPropertyChanged(nameof(TotalStudents));
                OnPropertyChanged(nameof(RegisteredStudents));
                OnPropertyChanged(nameof(InProgressCourses));
                OnPropertyChanged(nameof(CompletedCourses));
                OnPropertyChanged(nameof(ExamPending));
                OnPropertyChanged(nameof(ExamApplied));
                OnPropertyChanged(nameof(ExamCompleted));
                OnPropertyChanged(nameof(CertificatePending));
                OnPropertyChanged(nameof(CertificateSubmitted));
                OnPropertyChanged(nameof(CertificateVerified));
                OnPropertyChanged(nameof(CertificateReceived));
            }
        }
    }

    public string StaffDisplayName => !string.IsNullOrWhiteSpace(StaffProfile?.StaffName) ? StaffProfile.StaffName : "—";
    public string StaffDisplayId => !string.IsNullOrWhiteSpace(StaffProfile?.StaffIdentifier) ? StaffProfile.StaffIdentifier : "—";
    public string StaffDisplayDepartment => !string.IsNullOrWhiteSpace(StaffProfile?.Department) ? StaffProfile.Department : "—";
    public string StaffDisplayAssignedScope => StaffProfile != null
        ? $"Year {StaffProfile.AssignedYear} — Section {StaffProfile.AssignedClass ?? "All"}"
        : "—";

    public int TotalStudents => Summary.TotalStudents;
    public int RegisteredStudents => Summary.RegisteredStudents;
    public int InProgressCourses => Summary.InProgressCourses;
    public int CompletedCourses => Summary.CompletedCourses;
    public int ExamPending => Summary.ExamPending;
    public int ExamApplied => Summary.ExamApplied;
    public int ExamCompleted => Summary.ExamCompleted;
    public int CertificatePending => Summary.CertificatePending;
    public int CertificateSubmitted => Summary.CertificateSubmitted;
    public int CertificateVerified => Summary.CertificateVerified;
    public int CertificateReceived => Summary.CertificateReceived;

    public ICommand RefreshCommand { get; }
    public ICommand NavigateStudentsCommand { get; }
    public ICommand NavigateReportsCommand { get; }

    public StaffDashboardViewModel(IApiClient apiClient, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;

        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
        NavigateStudentsCommand = new RelayCommand(() => _navigationService.NavigateShellTo<StaffStudentsViewModel>());
        NavigateReportsCommand = new RelayCommand(() => _navigationService.NavigateShellTo<StaffReportsViewModel>());

        _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            // 1. Staff Profile
            StaffProfile = await _apiClient.GetStaffProfileAsync();

            // 2. Real Scoped Dashboard Summary
            Summary = await _apiClient.GetStaffDashboardSummaryAsync();
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Unable to load staff metrics from server.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
