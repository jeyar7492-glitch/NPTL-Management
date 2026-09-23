using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class AdminDashboardViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;

    private AdminProfileResponse? _adminData;

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

    public AdminProfileResponse? AdminData
    {
        get => _adminData;
        set
        {
            if (SetProperty(ref _adminData, value))
            {
                OnPropertyChanged(nameof(AdminIdentifier));
                OnPropertyChanged(nameof(TotalStudents));
                OnPropertyChanged(nameof(TotalStaff));
                OnPropertyChanged(nameof(TotalCourses));
                OnPropertyChanged(nameof(TotalRegistrations));
                OnPropertyChanged(nameof(TotalCertificates));
            }
        }
    }

    public string AdminIdentifier => !string.IsNullOrWhiteSpace(AdminData?.AdminIdentifier) ? AdminData.AdminIdentifier : "—";
    public int TotalStudents => AdminData?.TotalStudents ?? 0;
    public int TotalStaff => AdminData?.TotalStaff ?? 0;
    public int TotalCourses => AdminData?.TotalCourses ?? 0;
    public int TotalRegistrations => AdminData?.TotalRegistrations ?? 0;
    public int TotalCertificates => AdminData?.TotalCertificates ?? 0;

    public ICommand RefreshCommand { get; }

    public AdminDashboardViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(LoadAdminDataAsync);
    }

    public async Task LoadAdminDataAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var data = await _apiClient.GetAdminDashboardAsync();
            AdminData = data;
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Unable to load administration statistics from server.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
