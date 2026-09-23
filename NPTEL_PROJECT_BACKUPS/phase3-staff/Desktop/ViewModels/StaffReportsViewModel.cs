using System.Collections.ObjectModel;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StaffReportsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;

    private string _selectedReportType = "student-registration";
    private StaffReportPreviewDto? _reportPreview;
    private ObservableCollection<StaffReportItemDto> _items = new();

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

    public ObservableCollection<string> ReportTypes { get; } = new()
    {
        "Student Registration Report",
        "NPTEL Course Status Report",
        "Exam Status Report",
        "Certificate Status Report"
    };

    public string SelectedReportTypeDisplay
    {
        get => _selectedReportType switch
        {
            "student-registration" => "Student Registration Report",
            "course-status" => "NPTEL Course Status Report",
            "exam-status" => "Exam Status Report",
            "certificate-status" => "Certificate Status Report",
            _ => "Student Registration Report"
        };
        set
        {
            var code = value switch
            {
                "Student Registration Report" => "student-registration",
                "NPTEL Course Status Report" => "course-status",
                "Exam Status Report" => "exam-status",
                "Certificate Status Report" => "certificate-status",
                _ => "student-registration"
            };

            if (SetProperty(ref _selectedReportType, code))
            {
                OnPropertyChanged(nameof(SelectedReportTypeDisplay));
                _ = LoadReportAsync();
            }
        }
    }

    public StaffReportPreviewDto? ReportPreview
    {
        get => _reportPreview;
        set => SetProperty(ref _reportPreview, value);
    }

    public ObservableCollection<StaffReportItemDto> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public int TotalRecords => _items.Count;
    public bool HasNoRecords => !IsLoading && !HasError && _items.Count == 0;

    public ICommand RefreshCommand { get; }

    public StaffReportsViewModel(IApiClient apiClient, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;

        RefreshCommand = new AsyncRelayCommand(LoadReportAsync);

        _ = LoadReportAsync();
    }

    public async Task LoadReportAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var preview = await _apiClient.GetStaffReportPreviewAsync(_selectedReportType);
            ReportPreview = preview;

            Items.Clear();
            if (preview?.Items != null)
            {
                foreach (var item in preview.Items)
                {
                    Items.Add(item);
                }
            }
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Failed to generate report preview from server.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(HasNoRecords));
        }
    }
}
