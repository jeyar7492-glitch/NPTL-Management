using System.Windows;
using System.Windows.Input;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class SplashViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;

    private bool _isLoading;
    private bool _hasError;
    private string _statusMessage = "Initializing NPTEL Management System...";
    private string _errorMessage = string.Empty;

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

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand RetryCommand { get; }
    public ICommand ExitCommand { get; }

    public SplashViewModel(IApiClient apiClient, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;

        RetryCommand = new AsyncRelayCommand(PerformHealthCheckAsync);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
    }

    public async Task PerformHealthCheckAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = "Checking backend services and database connectivity...";
        ErrorMessage = string.Empty;

        try
        {
            // Give a brief moment for the splash animation to register
            await Task.Delay(500);

            var health = await _apiClient.GetHealthAsync();

            if (health.IsHealthy)
            {
                StatusMessage = "Connected successfully. Redirecting to login...";
                await Task.Delay(400);
                _navigationService.NavigateTo<LoginViewModel>();
            }
            else
            {
                HasError = true;
                if (health.Status == "offline" || health.Status == "unavailable")
                {
                    ErrorMessage = "Unable to connect to the NPTEL Management API server at " + ApiSettings.Instance.BaseUrl + ".\nPlease ensure the backend service is running.";
                }
                else if (health.Database != "connected")
                {
                    ErrorMessage = "API server is running, but the PostgreSQL database is currently unreachable or disconnected.\nStatus: " + health.Status + ", DB: " + health.Database;
                }
                else
                {
                    ErrorMessage = $"Backend service reported degraded status (Status: {health.Status}, Database: {health.Database}).";
                }
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Connection failure: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
