using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class AdminStudentsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private string? _statusMessage;

    // Filter properties
    private string _searchText = string.Empty;
    private string _selectedDepartment = "All";
    private string _selectedYear = "All";
    private string _selectedClass = "All";
    private string _selectedStatus = "All";

    private int _page = 1;
    private int _pageSize = 50;
    private int _totalCount;

    private ObservableCollection<AdminStudentListDto> _students = new();
    private AdminStudentListDto? _selectedStudent;

    // Dialog state
    private bool _isCreateDialogOpen;
    private CreateStudentDto _newStudent = new();

    private bool _isEditDialogOpen;
    private UpdateStudentDto _editStudent = new();
    private Guid _editingStudentId;

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
                _ = LoadStudentsAsync();
            }
        }
    }

    public string SelectedDepartment
    {
        get => _selectedDepartment;
        set
        {
            if (SetProperty(ref _selectedDepartment, value))
            {
                _page = 1;
                _ = LoadStudentsAsync();
            }
        }
    }

    public string SelectedYear
    {
        get => _selectedYear;
        set
        {
            if (SetProperty(ref _selectedYear, value))
            {
                _page = 1;
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
                _page = 1;
                _ = LoadStudentsAsync();
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
                _ = LoadStudentsAsync();
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public ObservableCollection<AdminStudentListDto> Students
    {
        get => _students;
        set => SetProperty(ref _students, value);
    }

    public AdminStudentListDto? SelectedStudent
    {
        get => _selectedStudent;
        set => SetProperty(ref _selectedStudent, value);
    }

    public bool IsCreateDialogOpen
    {
        get => _isCreateDialogOpen;
        set => SetProperty(ref _isCreateDialogOpen, value);
    }

    public CreateStudentDto NewStudent
    {
        get => _newStudent;
        set => SetProperty(ref _newStudent, value);
    }

    public bool IsEditDialogOpen
    {
        get => _isEditDialogOpen;
        set => SetProperty(ref _isEditDialogOpen, value);
    }

    public UpdateStudentDto EditStudent
    {
        get => _editStudent;
        set => SetProperty(ref _editStudent, value);
    }

    // Filter Options
    public ObservableCollection<string> DepartmentOptions { get; } = new() { "All", "CSE", "ECE", "MECH", "CIVIL" };
    public ObservableCollection<string> YearOptions { get; } = new() { "All", "1", "2", "3", "4" };
    public ObservableCollection<string> ClassOptions { get; } = new() { "All", "A", "B", "C" };
    public ObservableCollection<string> StatusOptions { get; } = new() { "All", "Active", "Inactive" };

    // Commands
    public ICommand RefreshCommand { get; }
    public ICommand OpenCreateDialogCommand { get; }
    public ICommand CloseCreateDialogCommand { get; }
    public ICommand SubmitCreateCommand { get; }
    public ICommand OpenEditDialogCommand { get; }
    public ICommand CloseEditDialogCommand { get; }
    public ICommand SubmitEditCommand { get; }
    public ICommand ToggleStatusCommand { get; }

    public AdminStudentsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        RefreshCommand = new AsyncRelayCommand(LoadStudentsAsync);
        OpenCreateDialogCommand = new RelayCommand(OpenCreateDialog);
        CloseCreateDialogCommand = new RelayCommand(() => IsCreateDialogOpen = false);
        SubmitCreateCommand = new AsyncRelayCommand(SubmitCreateAsync);
        OpenEditDialogCommand = new RelayCommand(OpenEditDialog);
        CloseEditDialogCommand = new RelayCommand(() => IsEditDialogOpen = false);
        SubmitEditCommand = new AsyncRelayCommand(SubmitEditAsync);
        ToggleStatusCommand = new AsyncRelayCommand(ToggleStatusAsync);

        _ = LoadStudentsAsync();
    }

    public async Task LoadStudentsAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            string? dept = SelectedDepartment == "All" ? null : SelectedDepartment;
            int? yr = int.TryParse(SelectedYear, out var y) ? y : null;
            string? cls = SelectedClass == "All" ? null : SelectedClass;
            bool? active = SelectedStatus == "All" ? null : (SelectedStatus == "Active");

            var result = await _apiClient.GetAdminStudentsAsync(
                SearchText, dept, yr, cls, active, _page, _pageSize);

            Students = new ObservableCollection<AdminStudentListDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load students: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenCreateDialog()
    {
        NewStudent = new CreateStudentDto
        {
            Department = "CSE",
            Year = 1,
            ClassSection = "A",
            Batch = $"{DateTime.UtcNow.Year}-{DateTime.UtcNow.Year + 4}",
            InitialPassword = "Student@123"
        };
        IsCreateDialogOpen = true;
    }

    private async Task SubmitCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewStudent.Name) || string.IsNullOrWhiteSpace(NewStudent.RegisterNumber))
        {
            MessageBox.Show("Please enter Student Name and Register Number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            await _apiClient.CreateAdminStudentAsync(NewStudent);
            IsCreateDialogOpen = false;
            StatusMessage = $"Student '{NewStudent.RegisterNumber}' created successfully.";
            await LoadStudentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create student: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenEditDialog()
    {
        if (SelectedStudent == null)
        {
            MessageBox.Show("Please select a student to edit.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _editingStudentId = SelectedStudent.StudentId;
        EditStudent = new UpdateStudentDto
        {
            Name = SelectedStudent.Name,
            Department = SelectedStudent.Department,
            Year = SelectedStudent.Year,
            ClassSection = SelectedStudent.ClassSection,
            Batch = SelectedStudent.Batch,
            Email = SelectedStudent.Email,
            Phone = SelectedStudent.Phone
        };
        IsEditDialogOpen = true;
    }

    private async Task SubmitEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditStudent.Name))
        {
            MessageBox.Show("Student Name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminStudentAsync(_editingStudentId, EditStudent);
            IsEditDialogOpen = false;
            StatusMessage = $"Student updated successfully.";
            await LoadStudentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update student: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleStatusAsync()
    {
        if (SelectedStudent == null)
        {
            MessageBox.Show("Please select a student first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var newStatus = !SelectedStudent.IsActive;
        var actionWord = newStatus ? "activate" : "deactivate";

        var confirm = MessageBox.Show(
            $"Are you sure you want to {actionWord} student '{SelectedStudent.RegisterNumber}' ({SelectedStudent.Name})?",
            "Confirm Status Change",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminStudentStatusAsync(SelectedStudent.StudentId, newStatus);
            StatusMessage = $"Student '{SelectedStudent.RegisterNumber}' status updated.";
            await LoadStudentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to change student status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
