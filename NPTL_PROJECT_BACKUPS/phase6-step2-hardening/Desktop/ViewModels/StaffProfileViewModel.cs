using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StaffProfileViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private StaffProfileResponse? _profile;

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

    public StaffProfileResponse? Profile
    {
        get => _profile;
        set => SetProperty(ref _profile, value);
    }

    public ICommand RefreshCommand { get; }

    public StaffProfileViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(LoadProfileAsync);
    }

    public async Task LoadProfileAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            Profile = await _apiClient.GetStaffProfileAsync();
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Failed to load staff profile.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
