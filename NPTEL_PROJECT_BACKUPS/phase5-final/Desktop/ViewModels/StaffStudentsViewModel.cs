using System.Collections.ObjectModel;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StaffStudentsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;

    private StaffProfileResponse? _staffProfile;
    private ObservableCollection<StaffStudentListDto> _students = new();
    private StaffStudentListDto? _selectedStudent;

    private string _searchText = string.Empty;
    private string _selectedClass = "All";
    private string _selectedCourse = "All";
    private string _selectedRegistrationStatus = "All";
    private string _selectedExamStatus = "All";
    private string _selectedCertificateStatus = "All";

    private ObservableCollection<string> _availableClasses = new() { "All" };
    private ObservableCollection<string> _availableCourses = new() { "All" };
    private ObservableCollection<string> _availableRegistrationStatuses = new() { "All", "Registered", "InProgress", "Completed" };
    private ObservableCollection<string> _availableExamStatuses = new() { "All", "NotStarted", "Applied", "Scheduled", "Completed" };
    private ObservableCollection<string> _availableCertificateStatuses = new() { "All", "Pending", "Submitted", "UnderVerification", "Verified", "Received" };

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
                OnPropertyChanged(nameof(StaffDisplayAssignedScope));
            }
        }
    }

    public string StaffDisplayName => StaffProfile?.StaffName ?? "—";
    public string StaffDisplayId => StaffProfile?.StaffIdentifier ?? "—";
    public string StaffDisplayAssignedScope => StaffProfile != null
        ? $"{StaffProfile.Department} — Year {StaffProfile.AssignedYear} — Section {StaffProfile.AssignedClass ?? "All"}"
        : "—";

    public ObservableCollection<StaffStudentListDto> Students
    {
        get => _students;
        set => SetProperty(ref _students, value);
    }

    public StaffStudentListDto? SelectedStudent
    {
        get => _selectedStudent;
        set => SetProperty(ref _selectedStudent, value);
    }

    public int StudentCount => _students.Count;
    public bool HasNoStudents => !IsLoading && !HasError && _students.Count == 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = LoadStudentsAsync();
            }
        }
    }

    public string SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetProperty(ref _selectedClass, value))
            {
                _ = LoadStudentsAsync();
            }
        }
    }

    public string SelectedCourse
    {
        get => _selectedCourse;
        set
        {
            if (SetProperty(ref _selectedCourse, value))
            {
                _ = LoadStudentsAsync();
            }
        }
    }

    public string SelectedRegistrationStatus
    {
        get => _selectedRegistrationStatus;
        set
        {
            if (SetProperty(ref _selectedRegistrationStatus, value))
            {
                _ = LoadStudentsAsync();
            }
        }
    }

    public string SelectedExamStatus
    {
        get => _selectedExamStatus;
        set
        {
            if (SetProperty(ref _selectedExamStatus, value))
            {
                _ = LoadStudentsAsync();
            }
        }
    }

    public string SelectedCertificateStatus
    {
        get => _selectedCertificateStatus;
        set
        {
            if (SetProperty(ref _selectedCertificateStatus, value))
            {
                _ = LoadStudentsAsync();
            }
        }
    }

    public ObservableCollection<string> AvailableClasses => _availableClasses;
    public ObservableCollection<string> AvailableCourses => _availableCourses;
    public ObservableCollection<string> AvailableRegistrationStatuses => _availableRegistrationStatuses;
    public ObservableCollection<string> AvailableExamStatuses => _availableExamStatuses;
    public ObservableCollection<string> AvailableCertificateStatuses => _availableCertificateStatuses;

    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand ViewDetailsCommand { get; }

    public StaffStudentsViewModel(IApiClient apiClient, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;

        RefreshCommand = new AsyncRelayCommand(LoadStudentsAsync);
        ClearFiltersCommand = new RelayCommand(ResetFilters);
        ViewDetailsCommand = new RelayCommand(param => OpenStudentDetails(param as StaffStudentListDto));

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            StaffProfile = await _apiClient.GetStaffProfileAsync();
        }
        catch
        {
            // Non-fatal, profile banner will fall back
        }

        await LoadStudentsAsync();
    }

    public async Task LoadStudentsAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var filter = new StaffStudentFilterDto
            {
                Search = !string.IsNullOrWhiteSpace(_searchText) ? _searchText.Trim() : null,
                ClassSection = _selectedClass != "All" ? _selectedClass : null,
                Course = _selectedCourse != "All" ? _selectedCourse : null,
                RegistrationStatus = _selectedRegistrationStatus != "All" ? _selectedRegistrationStatus : null,
                ExamStatus = _selectedExamStatus != "All" ? _selectedExamStatus : null,
                CertificateStatus = _selectedCertificateStatus != "All" ? _selectedCertificateStatus : null,
                Page = 1,
                PageSize = 100
            };

            var result = await _apiClient.GetStaffStudentsAsync(filter);
            Students.Clear();

            if (result?.Items != null)
            {
                foreach (var item in result.Items)
                {
                    Students.Add(item);
                }
            }

            // Update class options dynamically if not already populated
            UpdateFilterOptions();
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Failed to load authorized student records.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(StudentCount));
            OnPropertyChanged(nameof(HasNoStudents));
        }
    }

    private void UpdateFilterOptions()
    {
        var classes = Students
            .Select(s => s.ClassSection)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (classes.Count > 0 && AvailableClasses.Count <= 1)
        {
            foreach (var c in classes)
            {
                if (!AvailableClasses.Contains(c!)) AvailableClasses.Add(c!);
            }
        }
    }

    private void ResetFilters()
    {
        _searchText = string.Empty;
        _selectedClass = "All";
        _selectedCourse = "All";
        _selectedRegistrationStatus = "All";
        _selectedExamStatus = "All";
        _selectedCertificateStatus = "All";

        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedClass));
        OnPropertyChanged(nameof(SelectedCourse));
        OnPropertyChanged(nameof(SelectedRegistrationStatus));
        OnPropertyChanged(nameof(SelectedExamStatus));
        OnPropertyChanged(nameof(SelectedCertificateStatus));

        _ = LoadStudentsAsync();
    }

    private void OpenStudentDetails(StaffStudentListDto? student)
    {
        var targetStudent = student ?? SelectedStudent;
        if (targetStudent == null)
            return;

        var detailsVm = new StaffStudentDetailsViewModel(targetStudent.StudentId, _apiClient, _navigationService);
        _navigationService.NavigateShellTo(detailsVm);
    }
}
