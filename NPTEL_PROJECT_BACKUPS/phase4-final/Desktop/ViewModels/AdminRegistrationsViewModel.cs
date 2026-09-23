using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class AdminRegistrationsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private string? _statusMessage;

    private string _searchText = string.Empty;
    private string _selectedStatus = "All";

    private int _page = 1;
    private int _pageSize = 50;
    private int _totalCount;

    private ObservableCollection<AdminRegistrationDto> _registrations = new();
    private AdminRegistrationDto? _selectedRegistration;

    // Create Registration Dialog
    private bool _isCreateDialogOpen;
    private string _newStudentRegisterNumber = string.Empty;
    private string _newCourseCode = string.Empty;
    private ObservableCollection<AdminCourseDto> _availableCourses = new();
    private AdminCourseDto? _selectedNewCourse;

    // Edit Exam Dialog
    private bool _isExamDialogOpen;
    private UpdateAdminExamDto _editExam = new();
    private Guid _editingRegistrationId;
    private string _editingStudentInfo = string.Empty;
    private string _editingCourseInfo = string.Empty;

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

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _page = 1;
                _ = LoadRegistrationsAsync();
            }
        }
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                _page = 1;
                _ = LoadRegistrationsAsync();
            }
        }
    }

    public ObservableCollection<AdminRegistrationDto> Registrations
    {
        get => _registrations;
        set => SetProperty(ref _registrations, value);
    }

    public AdminRegistrationDto? SelectedRegistration
    {
        get => _selectedRegistration;
        set => SetProperty(ref _selectedRegistration, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public bool IsCreateDialogOpen
    {
        get => _isCreateDialogOpen;
        set => SetProperty(ref _isCreateDialogOpen, value);
    }

    public string NewStudentRegisterNumber
    {
        get => _newStudentRegisterNumber;
        set => SetProperty(ref _newStudentRegisterNumber, value);
    }

    public ObservableCollection<AdminCourseDto> AvailableCourses
    {
        get => _availableCourses;
        set => SetProperty(ref _availableCourses, value);
    }

    public AdminCourseDto? SelectedNewCourse
    {
        get => _selectedNewCourse;
        set => SetProperty(ref _selectedNewCourse, value);
    }

    public bool IsExamDialogOpen
    {
        get => _isExamDialogOpen;
        set => SetProperty(ref _isExamDialogOpen, value);
    }

    public UpdateAdminExamDto EditExam
    {
        get => _editExam;
        set => SetProperty(ref _editExam, value);
    }

    public string EditingStudentInfo
    {
        get => _editingStudentInfo;
        set => SetProperty(ref _editingStudentInfo, value);
    }

    public string EditingCourseInfo
    {
        get => _editingCourseInfo;
        set => SetProperty(ref _editingCourseInfo, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenCreateDialogCommand { get; }
    public ICommand SaveCreateCommand { get; }
    public ICommand CancelCreateCommand { get; }
    public ICommand OpenExamDialogCommand { get; }
    public ICommand SaveExamCommand { get; }
    public ICommand CancelExamCommand { get; }
    public ICommand UpdateStatusCommand { get; }

    public AdminRegistrationsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        RefreshCommand = new AsyncRelayCommand(LoadRegistrationsAsync);
        OpenCreateDialogCommand = new AsyncRelayCommand(OpenCreateDialogAsync);
        SaveCreateCommand = new AsyncRelayCommand(SaveCreateAsync);
        CancelCreateCommand = new RelayCommand(() => IsCreateDialogOpen = false);

        OpenExamDialogCommand = new AsyncRelayCommand<AdminRegistrationDto>(OpenExamDialogAsync);
        SaveExamCommand = new AsyncRelayCommand(SaveExamAsync);
        CancelExamCommand = new RelayCommand(() => IsExamDialogOpen = false);

        UpdateStatusCommand = new AsyncRelayCommand<AdminRegistrationDto>(UpdateStatusAsync);

        _ = LoadRegistrationsAsync();
    }

    public async Task LoadRegistrationsAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var statusFilter = _selectedStatus == "All" ? null : _selectedStatus;
            var search = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText;

            var result = await _apiClient.GetAdminRegistrationsAsync(search, statusFilter, null, null, _page, _pageSize);
            Registrations = new ObservableCollection<AdminRegistrationDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load registrations: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OpenCreateDialogAsync()
    {
        NewStudentRegisterNumber = string.Empty;
        SelectedNewCourse = null;

        try
        {
            var coursesResult = await _apiClient.GetAdminCoursesAsync(null, "Active", 1, 100);
            AvailableCourses = new ObservableCollection<AdminCourseDto>(coursesResult.Items);
            if (AvailableCourses.Count > 0)
            {
                SelectedNewCourse = AvailableCourses[0];
            }
            IsCreateDialogOpen = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load active courses: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewStudentRegisterNumber))
        {
            MessageBox.Show("Please enter the student's Register Number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedNewCourse == null)
        {
            MessageBox.Show("Please select an active course.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            // Lookup student by register number
            var studentsResult = await _apiClient.GetAdminStudentsAsync(NewStudentRegisterNumber.Trim(), null, null, null, null, 1, 1);
            var student = studentsResult.Items.FirstOrDefault(s => string.Equals(s.RegisterNumber, NewStudentRegisterNumber.Trim(), StringComparison.OrdinalIgnoreCase));
            if (student == null)
            {
                MessageBox.Show($"Student with Register Number '{NewStudentRegisterNumber}' not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dto = new CreateRegistrationDto
            {
                StudentId = student.StudentId,
                CourseId = SelectedNewCourse.CourseId,
                EnrollmentDate = DateTime.UtcNow,
                Status = "Registered"
            };

            await _apiClient.CreateAdminRegistrationAsync(dto);
            IsCreateDialogOpen = false;
            StatusMessage = $"Enrolled {student.Name} into {SelectedNewCourse.CourseCode} successfully.";
            await LoadRegistrationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Enrollment failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OpenExamDialogAsync(AdminRegistrationDto? reg)
    {
        var target = reg ?? SelectedRegistration;
        if (target == null) return;

        _editingRegistrationId = target.RegistrationId;
        EditingStudentInfo = $"{target.StudentName} ({target.RegisterNumber})";
        EditingCourseInfo = $"{target.CourseCode} - {target.CourseName}";

        IsLoading = true;
        try
        {
            var exam = await _apiClient.GetAdminExamByRegistrationIdAsync(target.RegistrationId);
            EditExam = new UpdateAdminExamDto
            {
                ExamApplicationStatus = exam.ExamApplicationStatus,
                ExamApplicationDate = exam.ExamApplicationDate,
                ExamApplicationDeadline = exam.ExamApplicationDeadline,
                ExamDate = exam.ExamDate,
                HallTicketStatus = exam.HallTicketStatus,
                ExamStatus = exam.ExamStatus,
                Score = exam.Score,
                PassStatus = exam.PassStatus
            };
            IsExamDialogOpen = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to retrieve exam record: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveExamAsync()
    {
        if (EditExam.Score.HasValue && (EditExam.Score < 0 || EditExam.Score > 100))
        {
            MessageBox.Show("Exam score must be between 0 and 100.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminExamAsync(_editingRegistrationId, EditExam);
            IsExamDialogOpen = false;
            StatusMessage = "Exam record updated successfully.";
            await LoadRegistrationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update exam details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateStatusAsync(AdminRegistrationDto? reg)
    {
        var target = reg ?? SelectedRegistration;
        if (target == null) return;

        var nextStatus = target.Status switch
        {
            "Registered" => "InProgress",
            "InProgress" => "Completed",
            "Completed" => "Dropped",
            _ => "Registered"
        };

        var confirm = MessageBox.Show(
            $"Change registration status for '{target.StudentName} - {target.CourseCode}' to '{nextStatus}'?",
            "Update Registration Status",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminRegistrationStatusAsync(target.RegistrationId, nextStatus);
            StatusMessage = $"Updated registration status to {nextStatus}.";
            await LoadRegistrationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
