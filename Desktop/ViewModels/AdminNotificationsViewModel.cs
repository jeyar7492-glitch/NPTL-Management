using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class AdminNotificationsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private string? _statusMessage;

    private string _searchText = string.Empty;
    private int _page = 1;
    private int _pageSize = 50;
    private int _totalCount;

    private ObservableCollection<AdminNotificationDto> _notifications = new();
    private AdminNotificationDto? _selectedNotification;

    // Compose Dialog
    private bool _isComposeDialogOpen;
    private CreateNotificationDto _newNotification = new();
    private string _individualRegisterNumber = string.Empty;

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
                _ = LoadNotificationsAsync();
            }
        }
    }

    public ObservableCollection<AdminNotificationDto> Notifications
    {
        get => _notifications;
        set => SetProperty(ref _notifications, value);
    }

    public AdminNotificationDto? SelectedNotification
    {
        get => _selectedNotification;
        set => SetProperty(ref _selectedNotification, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public bool IsComposeDialogOpen
    {
        get => _isComposeDialogOpen;
        set => SetProperty(ref _isComposeDialogOpen, value);
    }

    public CreateNotificationDto NewNotification
    {
        get => _newNotification;
        set => SetProperty(ref _newNotification, value);
    }

    public string IndividualRegisterNumber
    {
        get => _individualRegisterNumber;
        set => SetProperty(ref _individualRegisterNumber, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenComposeDialogCommand { get; }
    public ICommand SendNotificationCommand { get; }
    public ICommand CancelComposeCommand { get; }
    public ICommand DeleteNotificationCommand { get; }
    public ICommand TriggerRulesCommand { get; }

    public AdminNotificationsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        RefreshCommand = new AsyncRelayCommand(LoadNotificationsAsync);
        OpenComposeDialogCommand = new RelayCommand(OpenComposeDialog);
        SendNotificationCommand = new AsyncRelayCommand(SendNotificationAsync);
        CancelComposeCommand = new RelayCommand(() => IsComposeDialogOpen = false);
        DeleteNotificationCommand = new AsyncRelayCommand<AdminNotificationDto>(DeleteNotificationAsync);
        TriggerRulesCommand = new AsyncRelayCommand(TriggerRulesAsync);

        _ = LoadNotificationsAsync();
    }

    public async Task LoadNotificationsAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var search = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText;
            var result = await _apiClient.GetAdminNotificationsAsync(search, _page, _pageSize);
            Notifications = new ObservableCollection<AdminNotificationDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load notifications: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenComposeDialog()
    {
        IndividualRegisterNumber = string.Empty;
        NewNotification = new CreateNotificationDto
        {
            Title = string.Empty,
            Message = string.Empty,
            TargetType = "AllDepartment",
            Department = "CSE",
            Year = 1,
            ClassSection = "A"
        };
        IsComposeDialogOpen = true;
    }

    private async Task SendNotificationAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNotification.Title) || string.IsNullOrWhiteSpace(NewNotification.Message))
        {
            MessageBox.Show("Please provide both Title and Message for the broadcast.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            if (NewNotification.TargetType == "Individual")
            {
                if (string.IsNullOrWhiteSpace(IndividualRegisterNumber))
                {
                    MessageBox.Show("Please enter the recipient student's Register Number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var studentsResult = await _apiClient.GetAdminStudentsAsync(IndividualRegisterNumber.Trim(), null, null, null, null, 1, 1);
                var student = studentsResult.Items.FirstOrDefault(s => string.Equals(s.RegisterNumber, IndividualRegisterNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                if (student == null)
                {
                    MessageBox.Show($"Student with Register Number '{IndividualRegisterNumber}' not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                NewNotification.TargetUserId = student.UserId;
            }

            var count = await _apiClient.CreateAdminNotificationAsync(NewNotification);
            IsComposeDialogOpen = false;
            StatusMessage = $"Broadcast dispatched to {count} recipient(s).";
            await LoadNotificationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Broadcast failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteNotificationAsync(AdminNotificationDto? notif)
    {
        var target = notif ?? SelectedNotification;
        if (target == null) return;

        var confirm = MessageBox.Show(
            $"Delete notification '{target.Title}' sent to {target.RecipientName}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            await _apiClient.DeleteAdminNotificationAsync(target.NotificationId);
            StatusMessage = "Notification removed.";
            await LoadNotificationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete notification: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task TriggerRulesAsync()
    {
        IsLoading = true;
        try
        {
            StatusMessage = "Evaluating server-side automation rules engine...";
            var generatedCount = await _apiClient.TriggerAdminNotificationRulesAsync();
            StatusMessage = $"Automation cycle complete. {generatedCount} reminder notification(s) generated.";
            await LoadNotificationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to execute automation rules: {ex.Message}", "Automation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
