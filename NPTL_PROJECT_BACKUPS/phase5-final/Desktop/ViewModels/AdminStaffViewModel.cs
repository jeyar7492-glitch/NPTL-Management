using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class AdminStaffViewModel : ViewModelBase
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

    private ObservableCollection<AdminStaffListDto> _staffMembers = new();
    private AdminStaffListDto? _selectedStaff;

    // Create Modal
    private bool _isCreateDialogOpen;
    private CreateStaffDto _newStaff = new();

    // Edit Modal
    private bool _isEditDialogOpen;
    private UpdateStaffDto _editStaff = new();
    private Guid _editingStaffId;

    // Reset Password Modal
    private bool _isResetPasswordDialogOpen;
    private string _newPassword = string.Empty;

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
                _ = LoadStaffAsync();
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
                _ = LoadStaffAsync();
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
                _ = LoadStaffAsync();
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
                _ = LoadStaffAsync();
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
                _ = LoadStaffAsync();
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public ObservableCollection<AdminStaffListDto> StaffMembers
    {
        get => _staffMembers;
        set => SetProperty(ref _staffMembers, value);
    }

    public AdminStaffListDto? SelectedStaff
    {
        get => _selectedStaff;
        set => SetProperty(ref _selectedStaff, value);
    }

    public bool IsCreateDialogOpen
    {
        get => _isCreateDialogOpen;
        set => SetProperty(ref _isCreateDialogOpen, value);
    }

    public CreateStaffDto NewStaff
    {
        get => _newStaff;
        set => SetProperty(ref _newStaff, value);
    }

    public bool IsEditDialogOpen
    {
        get => _isEditDialogOpen;
        set => SetProperty(ref _isEditDialogOpen, value);
    }

    public UpdateStaffDto EditStaff
    {
        get => _editStaff;
        set => SetProperty(ref _editStaff, value);
    }

    public bool IsResetPasswordDialogOpen
    {
        get => _isResetPasswordDialogOpen;
        set => SetProperty(ref _isResetPasswordDialogOpen, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

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
    public ICommand OpenResetPasswordDialogCommand { get; }
    public ICommand CloseResetPasswordDialogCommand { get; }
    public ICommand SubmitResetPasswordCommand { get; }

    public AdminStaffViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        RefreshCommand = new AsyncRelayCommand(LoadStaffAsync);
        OpenCreateDialogCommand = new RelayCommand(OpenCreateDialog);
        CloseCreateDialogCommand = new RelayCommand(() => IsCreateDialogOpen = false);
        SubmitCreateCommand = new AsyncRelayCommand(SubmitCreateAsync);
        OpenEditDialogCommand = new RelayCommand(OpenEditDialog);
        CloseEditDialogCommand = new RelayCommand(() => IsEditDialogOpen = false);
        SubmitEditCommand = new AsyncRelayCommand(SubmitEditAsync);
        ToggleStatusCommand = new AsyncRelayCommand(ToggleStatusAsync);
        OpenResetPasswordDialogCommand = new RelayCommand(OpenResetPasswordDialog);
        CloseResetPasswordDialogCommand = new RelayCommand(() => IsResetPasswordDialogOpen = false);
        SubmitResetPasswordCommand = new AsyncRelayCommand(SubmitResetPasswordAsync);

        _ = LoadStaffAsync();
    }

    public async Task LoadStaffAsync()
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

            var result = await _apiClient.GetAdminStaffAsync(
                SearchText, dept, yr, cls, active, _page, _pageSize);

            StaffMembers = new ObservableCollection<AdminStaffListDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load staff members: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenCreateDialog()
    {
        NewStaff = new CreateStaffDto
        {
            Department = "CSE",
            AssignedYear = 1,
            AssignedClass = "A",
            InitialPassword = "Staff@123"
        };
        IsCreateDialogOpen = true;
    }

    private async Task SubmitCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewStaff.StaffName) || string.IsNullOrWhiteSpace(NewStaff.StaffIdentifier))
        {
            MessageBox.Show("Please enter Staff Name and Identifier.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            await _apiClient.CreateAdminStaffAsync(NewStaff);
            IsCreateDialogOpen = false;
            StatusMessage = $"Staff member '{NewStaff.StaffName}' created successfully.";
            await LoadStaffAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create staff member: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenEditDialog()
    {
        if (SelectedStaff == null)
        {
            MessageBox.Show("Please select a staff member to edit.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _editingStaffId = SelectedStaff.StaffId;
        EditStaff = new UpdateStaffDto
        {
            StaffName = SelectedStaff.StaffName,
            Department = SelectedStaff.Department,
            AssignedYear = SelectedStaff.AssignedYear,
            AssignedClass = SelectedStaff.AssignedClass,
            Email = SelectedStaff.Email
        };
        IsEditDialogOpen = true;
    }

    private async Task SubmitEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditStaff.StaffName))
        {
            MessageBox.Show("Staff Name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminStaffAsync(_editingStaffId, EditStaff);
            IsEditDialogOpen = false;
            StatusMessage = "Staff details updated successfully.";
            await LoadStaffAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update staff: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleStatusAsync()
    {
        if (SelectedStaff == null)
        {
            MessageBox.Show("Please select a staff member first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var newStatus = !SelectedStaff.IsActive;
        var actionWord = newStatus ? "activate" : "deactivate";

        var confirm = MessageBox.Show(
            $"Are you sure you want to {actionWord} staff member '{SelectedStaff.StaffName}' ({SelectedStaff.StaffIdentifier})?",
            "Confirm Status Change",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminStaffStatusAsync(SelectedStaff.StaffId, newStatus);
            StatusMessage = $"Staff '{SelectedStaff.StaffName}' status updated.";
            await LoadStaffAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to change staff status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenResetPasswordDialog()
    {
        if (SelectedStaff == null)
        {
            MessageBox.Show("Please select a staff member to reset password.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        NewPassword = "Staff@NewPassword123";
        IsResetPasswordDialogOpen = true;
    }

    private async Task SubmitResetPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            MessageBox.Show("Password must be at least 6 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedStaff == null) return;

        IsLoading = true;
        try
        {
            await _apiClient.ResetAdminStaffPasswordAsync(SelectedStaff.StaffId, NewPassword);
            IsResetPasswordDialogOpen = false;
            StatusMessage = $"Password for '{SelectedStaff.StaffName}' reset successfully.";
            MessageBox.Show($"Password for '{SelectedStaff.StaffName}' has been reset successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to reset password: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
