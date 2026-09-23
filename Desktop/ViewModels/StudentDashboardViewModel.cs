using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StudentDashboardViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private StudentProfileResponse? _profile;

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

    public StudentProfileResponse? Profile
    {
        get => _profile;
        set
        {
            if (SetProperty(ref _profile, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DisplayRegisterNumber));
                OnPropertyChanged(nameof(DisplayDepartment));
                OnPropertyChanged(nameof(DisplayClassSection));
                OnPropertyChanged(nameof(DisplayYear));
                OnPropertyChanged(nameof(DisplayBatch));
                OnPropertyChanged(nameof(DisplayEmail));
                OnPropertyChanged(nameof(DisplayPhone));
            }
        }
    }

    public string DisplayName => !string.IsNullOrWhiteSpace(Profile?.Name) ? Profile.Name : "—";
    public string DisplayRegisterNumber => !string.IsNullOrWhiteSpace(Profile?.RegisterNumber) ? Profile.RegisterNumber : "—";
    public string DisplayDepartment => !string.IsNullOrWhiteSpace(Profile?.Department) ? Profile.Department : "—";
    public string DisplayClassSection => !string.IsNullOrWhiteSpace(Profile?.ClassSection) ? Profile.ClassSection : "—";
    public string DisplayYear => Profile?.Year > 0 ? $"Year {Profile.Year}" : "—";
    public string DisplayBatch => !string.IsNullOrWhiteSpace(Profile?.Batch) ? Profile.Batch : "—";
    public string DisplayEmail => !string.IsNullOrWhiteSpace(Profile?.Email) ? Profile.Email : "Not provided";
    public string DisplayPhone => !string.IsNullOrWhiteSpace(Profile?.Phone) ? Profile.Phone : "Not provided";

    public ICommand RefreshCommand { get; }

    public StudentDashboardViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(LoadStudentProfileAsync);
    }

    public async Task LoadStudentProfileAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var data = await _apiClient.GetStudentProfileAsync();
            Profile = data;
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Unable to load student profile details.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
