using System.Collections.ObjectModel;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class AdminAuditLogsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;

    private string _searchText = string.Empty;
    private string _selectedAction = "All";
    private string _selectedRole = "All";
    private DateTime? _dateFrom;
    private DateTime? _dateTo;

    private int _page = 1;
    private int _pageSize = 50;
    private int _totalCount;

    private ObservableCollection<AuditLogDto> _auditLogs = new();
    private AuditLogDto? _selectedLog;

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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _page = 1;
                _ = LoadAuditLogsAsync();
            }
        }
    }

    public string SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (SetProperty(ref _selectedAction, value))
            {
                _page = 1;
                _ = LoadAuditLogsAsync();
            }
        }
    }

    public string SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetProperty(ref _selectedRole, value))
            {
                _page = 1;
                _ = LoadAuditLogsAsync();
            }
        }
    }

    public DateTime? DateFrom
    {
        get => _dateFrom;
        set
        {
            if (SetProperty(ref _dateFrom, value))
            {
                _page = 1;
                _ = LoadAuditLogsAsync();
            }
        }
    }

    public DateTime? DateTo
    {
        get => _dateTo;
        set
        {
            if (SetProperty(ref _dateTo, value))
            {
                _page = 1;
                _ = LoadAuditLogsAsync();
            }
        }
    }

    public ObservableCollection<AuditLogDto> AuditLogs
    {
        get => _auditLogs;
        set => SetProperty(ref _auditLogs, value);
    }

    public AuditLogDto? SelectedLog
    {
        get => _selectedLog;
        set => SetProperty(ref _selectedLog, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public ICommand RefreshCommand { get; }

    public AdminAuditLogsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(LoadAuditLogsAsync);
        _ = LoadAuditLogsAsync();
    }

    public async Task LoadAuditLogsAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var filter = new AuditLogFilterDto
            {
                Search = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText.Trim(),
                Action = _selectedAction == "All" ? null : _selectedAction,
                Role = _selectedRole == "All" ? null : _selectedRole,
                DateFrom = _dateFrom,
                DateTo = _dateTo,
                Page = _page,
                PageSize = _pageSize
            };

            var result = await _apiClient.GetAdminAuditLogsAsync(filter);
            AuditLogs = new ObservableCollection<AuditLogDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load audit logs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
