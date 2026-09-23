using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class AdminCoursesViewModel : ViewModelBase
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

    private ObservableCollection<AdminCourseDto> _courses = new();
    private AdminCourseDto? _selectedCourse;

    // Create dialog
    private bool _isCreateDialogOpen;
    private CreateCourseDto _newCourse = new();

    // Edit dialog
    private bool _isEditDialogOpen;
    private UpdateCourseDto _editCourse = new();
    private Guid _editingCourseId;

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
                _ = LoadCoursesAsync();
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
                _ = LoadCoursesAsync();
            }
        }
    }

    public ObservableCollection<AdminCourseDto> Courses
    {
        get => _courses;
        set => SetProperty(ref _courses, value);
    }

    public AdminCourseDto? SelectedCourse
    {
        get => _selectedCourse;
        set => SetProperty(ref _selectedCourse, value);
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

    public CreateCourseDto NewCourse
    {
        get => _newCourse;
        set => SetProperty(ref _newCourse, value);
    }

    public bool IsEditDialogOpen
    {
        get => _isEditDialogOpen;
        set => SetProperty(ref _isEditDialogOpen, value);
    }

    public UpdateCourseDto EditCourse
    {
        get => _editCourse;
        set => SetProperty(ref _editCourse, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenCreateDialogCommand { get; }
    public ICommand SaveCreateCommand { get; }
    public ICommand CancelCreateCommand { get; }
    public ICommand OpenEditDialogCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand ToggleStatusCommand { get; }

    public AdminCoursesViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        RefreshCommand = new AsyncRelayCommand(LoadCoursesAsync);
        OpenCreateDialogCommand = new RelayCommand(OpenCreateDialog);
        SaveCreateCommand = new AsyncRelayCommand(SaveCreateAsync);
        CancelCreateCommand = new RelayCommand(() => IsCreateDialogOpen = false);

        OpenEditDialogCommand = new RelayCommand<AdminCourseDto>(OpenEditDialog);
        SaveEditCommand = new AsyncRelayCommand(SaveEditAsync);
        CancelEditCommand = new RelayCommand(() => IsEditDialogOpen = false);

        ToggleStatusCommand = new AsyncRelayCommand<AdminCourseDto>(ToggleStatusAsync);

        _ = LoadCoursesAsync();
    }

    public async Task LoadCoursesAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var statusFilter = _selectedStatus == "All" ? null : _selectedStatus;
            var search = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText;

            var result = await _apiClient.GetAdminCoursesAsync(search, statusFilter, _page, _pageSize);
            Courses = new ObservableCollection<AdminCourseDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load courses: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenCreateDialog()
    {
        NewCourse = new CreateCourseDto
        {
            CourseCode = string.Empty,
            CourseName = string.Empty,
            DurationWeeks = 12,
            Status = "Active",
            CourseStartDate = DateTime.Today.AddDays(7),
            CourseEndDate = DateTime.Today.AddDays(91)
        };
        IsCreateDialogOpen = true;
    }

    private async Task SaveCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCourse.CourseCode) || string.IsNullOrWhiteSpace(NewCourse.CourseName))
        {
            MessageBox.Show("Please provide both Course Code and Course Name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            await _apiClient.CreateAdminCourseAsync(NewCourse);
            IsCreateDialogOpen = false;
            StatusMessage = $"Course '{NewCourse.CourseCode}' added successfully.";
            await LoadCoursesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create course: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenEditDialog(AdminCourseDto? course)
    {
        var target = course ?? SelectedCourse;
        if (target == null) return;

        _editingCourseId = target.CourseId;
        EditCourse = new UpdateCourseDto
        {
            CourseName = target.CourseName,
            DurationWeeks = target.DurationWeeks,
            CourseStartDate = target.CourseStartDate,
            CourseEndDate = target.CourseEndDate,
            Status = target.Status
        };
        IsEditDialogOpen = true;
    }

    private async Task SaveEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditCourse.CourseName))
        {
            MessageBox.Show("Please provide a Course Name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminCourseAsync(_editingCourseId, EditCourse);
            IsEditDialogOpen = false;
            StatusMessage = "Course details updated successfully.";
            await LoadCoursesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update course: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleStatusAsync(AdminCourseDto? course)
    {
        var target = course ?? SelectedCourse;
        if (target == null) return;

        var newStatus = target.Status == "Active" ? "Archived" : "Active";
        var confirmMsg = target.Status == "Active"
            ? $"Are you sure you want to archive course '{target.CourseCode} - {target.CourseName}'? Existing registrations will be preserved."
            : $"Are you sure you want to reactivate course '{target.CourseCode}'?";

        var result = MessageBox.Show(confirmMsg, "Confirm Status Change", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminCourseStatusAsync(target.CourseId, newStatus);
            StatusMessage = $"Course '{target.CourseCode}' status updated to {newStatus}.";
            await LoadCoursesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update course status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
