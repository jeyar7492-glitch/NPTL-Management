using System.Collections.ObjectModel;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StaffDashboardViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;

    private StaffProfileResponse? _staffProfile;
    private List<StudentProfileResponse> _allStudents = new();
    private ObservableCollection<StudentProfileResponse> _filteredStudents = new();

    private string _searchText = string.Empty;
    private string _selectedClassFilter = "All";
    private string _selectedYearFilter = "All";
    private string _selectedDepartmentFilter = "All";

    private ObservableCollection<string> _availableClasses = new() { "All" };
    private ObservableCollection<string> _availableYears = new() { "All" };
    private ObservableCollection<string> _availableDepartments = new() { "All" };

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

    public StaffProfileResponse? StaffProfile
    {
        get => _staffProfile;
        set
        {
            if (SetProperty(ref _staffProfile, value))
            {
                OnPropertyChanged(nameof(StaffDisplayName));
                OnPropertyChanged(nameof(StaffDisplayId));
                OnPropertyChanged(nameof(StaffDisplayDepartment));
                OnPropertyChanged(nameof(StaffDisplayAssignedScope));
            }
        }
    }

    public string StaffDisplayName => !string.IsNullOrWhiteSpace(StaffProfile?.StaffName) ? StaffProfile.StaffName : "—";
    public string StaffDisplayId => !string.IsNullOrWhiteSpace(StaffProfile?.StaffIdentifier) ? StaffProfile.StaffIdentifier : "—";
    public string StaffDisplayDepartment => !string.IsNullOrWhiteSpace(StaffProfile?.Department) ? StaffProfile.Department : "—";
    public string StaffDisplayAssignedScope => StaffProfile != null
        ? $"Year {StaffProfile.AssignedYear} — Section {StaffProfile.AssignedClass ?? "All"}"
        : "—";

    public ObservableCollection<StudentProfileResponse> FilteredStudents
    {
        get => _filteredStudents;
        set => SetProperty(ref _filteredStudents, value);
    }

    public int StudentCount => _filteredStudents.Count;
    public bool HasNoStudents => !IsLoading && !HasError && _filteredStudents.Count == 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedClassFilter
    {
        get => _selectedClassFilter;
        set
        {
            if (SetProperty(ref _selectedClassFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedYearFilter
    {
        get => _selectedYearFilter;
        set
        {
            if (SetProperty(ref _selectedYearFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedDepartmentFilter
    {
        get => _selectedDepartmentFilter;
        set
        {
            if (SetProperty(ref _selectedDepartmentFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public ObservableCollection<string> AvailableClasses
    {
        get => _availableClasses;
        set => SetProperty(ref _availableClasses, value);
    }

    public ObservableCollection<string> AvailableYears
    {
        get => _availableYears;
        set => SetProperty(ref _availableYears, value);
    }

    public ObservableCollection<string> AvailableDepartments
    {
        get => _availableDepartments;
        set => SetProperty(ref _availableDepartments, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public StaffDashboardViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
        ClearSearchCommand = new RelayCommand(() =>
        {
            SearchText = string.Empty;
            SelectedClassFilter = "All";
            SelectedYearFilter = "All";
            SelectedDepartmentFilter = "All";
        });
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            // 1. Load Staff Profile
            var staff = await _apiClient.GetStaffProfileAsync();
            StaffProfile = staff;

            // 2. Load Assigned Students
            var students = await _apiClient.GetAssignedStudentsAsync();
            _allStudents = students ?? new List<StudentProfileResponse>();

            // Populate Filter options
            UpdateFilterOptions();

            // Apply active filters
            ApplyFilters();
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Unable to load staff records from server.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoStudents));
            OnPropertyChanged(nameof(StudentCount));
        }
    }

    private void UpdateFilterOptions()
    {
        var classes = _allStudents
            .Select(s => s.ClassSection)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        AvailableClasses.Clear();
        AvailableClasses.Add("All");
        foreach (var c in classes) AvailableClasses.Add(c!);

        var years = _allStudents
            .Select(s => s.Year.ToString())
            .Where(y => y != "0")
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        AvailableYears.Clear();
        AvailableYears.Add("All");
        foreach (var y in years) AvailableYears.Add($"Year {y}");

        var depts = _allStudents
            .Select(s => s.Department)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        AvailableDepartments.Clear();
        AvailableDepartments.Add("All");
        foreach (var d in depts) AvailableDepartments.Add(d);

        SelectedClassFilter = "All";
        SelectedYearFilter = "All";
        SelectedDepartmentFilter = "All";
    }

    private void ApplyFilters()
    {
        var query = _allStudents.AsEnumerable();

        // 1. Text Search (Name, Register Number, Class Section)
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var term = _searchText.Trim().ToLowerInvariant();
            query = query.Where(s =>
                (!string.IsNullOrWhiteSpace(s.Name) && s.Name.ToLowerInvariant().Contains(term)) ||
                (!string.IsNullOrWhiteSpace(s.RegisterNumber) && s.RegisterNumber.ToLowerInvariant().Contains(term)) ||
                (!string.IsNullOrWhiteSpace(s.ClassSection) && s.ClassSection.ToLowerInvariant().Contains(term))
            );
        }

        // 2. Class Section Filter
        if (SelectedClassFilter != "All")
        {
            query = query.Where(s => string.Equals(s.ClassSection, SelectedClassFilter, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Year Filter
        if (SelectedYearFilter != "All" && SelectedYearFilter.StartsWith("Year "))
        {
            if (int.TryParse(SelectedYearFilter.Replace("Year ", "").Trim(), out var y))
            {
                query = query.Where(s => s.Year == y);
            }
        }

        // 4. Department Filter
        if (SelectedDepartmentFilter != "All")
        {
            query = query.Where(s => string.Equals(s.Department, SelectedDepartmentFilter, StringComparison.OrdinalIgnoreCase));
        }

        FilteredStudents.Clear();
        foreach (var student in query)
        {
            FilteredStudents.Add(student);
        }

        OnPropertyChanged(nameof(StudentCount));
        OnPropertyChanged(nameof(HasNoStudents));
    }
}
