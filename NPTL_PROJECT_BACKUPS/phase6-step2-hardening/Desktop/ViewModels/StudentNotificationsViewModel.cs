using System.Collections.ObjectModel;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StudentNotificationsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private ObservableCollection<StudentNotificationDto> _notifications = new();

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

    public ObservableCollection<StudentNotificationDto> Notifications
    {
        get => _notifications;
        set
        {
            if (SetProperty(ref _notifications, value))
            {
                OnPropertyChanged(nameof(HasNotifications));
                OnPropertyChanged(nameof(HasNoNotifications));
            }
        }
    }

    public bool HasNotifications => Notifications.Count > 0;
    public bool HasNoNotifications => !IsLoading && Notifications.Count == 0;

    public ICommand RefreshCommand { get; }
    public ICommand MarkAsReadCommand { get; }

    public StudentNotificationsViewModel(IApiClient apiClient, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;

        RefreshCommand = new AsyncRelayCommand(LoadNotificationsAsync);
        MarkAsReadCommand = new AsyncRelayCommand(async obj => await MarkAsReadAsync(obj as StudentNotificationDto));
    }

    public async Task LoadNotificationsAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var data = await _apiClient.GetStudentNotificationsAsync();
            Notifications = new ObservableCollection<StudentNotificationDto>(data);
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Unable to load student notifications.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNotifications));
            OnPropertyChanged(nameof(HasNoNotifications));
        }
    }

    private async Task MarkAsReadAsync(StudentNotificationDto? notif)
    {
        if (notif == null || notif.IsRead) return;

        try
        {
            await _apiClient.MarkNotificationAsReadAsync(notif.NotificationId);
            notif.IsRead = true;
            // Refresh list to trigger UI update
            Notifications = new ObservableCollection<StudentNotificationDto>(Notifications);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to mark notification as read: {ex.Message}";
        }
    }
}
