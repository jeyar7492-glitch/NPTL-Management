using System.Collections.ObjectModel;
using System.Windows.Input;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class NavigationItemModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _icon = string.Empty;
    private bool _isSelected;
    private ICommand? _command;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ICommand? Command
    {
        get => _command;
        set => SetProperty(ref _command, value);
    }
}

public class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationSession _session;
    private readonly IApiClient _apiClient;

    private ViewModelBase? _currentView;
    private ObservableCollection<NavigationItemModel> _navigationItems = new();
    private NavigationItemModel? _selectedItem;

    public ViewModelBase? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public ObservableCollection<NavigationItemModel> NavigationItems
    {
        get => _navigationItems;
        set => SetProperty(ref _navigationItems, value);
    }

    public NavigationItemModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value?.Command != null)
            {
                foreach (var item in NavigationItems)
                {
                    item.IsSelected = (item == value);
                }
                value.Command.Execute(null);
            }
        }
    }

    public string UserDisplayName => !string.IsNullOrWhiteSpace(_session.UserName) ? _session.UserName : "User";
    public string UserRole => !string.IsNullOrWhiteSpace(_session.UserRole) ? _session.UserRole : "Unknown";
    public string UserIdentifier => !string.IsNullOrWhiteSpace(_session.Identifier) ? _session.Identifier : "—";

    public bool IsStudent => string.Equals(UserRole, "Student", StringComparison.OrdinalIgnoreCase);
    public bool IsStaff => string.Equals(UserRole, "Staff", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin => string.Equals(UserRole, "Admin", StringComparison.OrdinalIgnoreCase);

    public ICommand LogoutCommand { get; }

    public MainViewModel(INavigationService navigationService, IAuthenticationSession session, IApiClient apiClient)
    {
        _navigationService = navigationService;
        _session = session;
        _apiClient = apiClient;

        LogoutCommand = new AsyncRelayCommand(ExecuteLogoutAsync);

        _navigationService.ShellNavigationChanged += () =>
        {
            CurrentView = _navigationService.CurrentShellView;
        };

        InitializeNavigation();
    }

    private void InitializeNavigation()
    {
        NavigationItems.Clear();

        if (IsStudent)
        {
            AddNavItem("Dashboard", "📊", () => _navigationService.NavigateShellTo<StudentDashboardViewModel>());
            AddNavItem("Courses", "📚", () => _navigationService.NavigateShellTo<StudentCoursesViewModel>());
            AddNavItem("Notifications", "🔔", () => _navigationService.NavigateShellTo<StudentNotificationsViewModel>());
            AddNavItem("Profile", "👤", () => _navigationService.NavigateShellTo<StudentProfileViewModel>());

            // Default to Student Dashboard
            _navigationService.NavigateShellTo<StudentDashboardViewModel>();
        }
        else if (IsStaff)
        {
            AddNavItem("Dashboard", "📊", () => _navigationService.NavigateShellTo<StaffDashboardViewModel>());
            AddNavItem("Students", "👥", () => _navigationService.NavigateShellTo<StaffDashboardViewModel>());
            AddNavItem("Reports", "📈", () => _navigationService.NavigateShellTo(new PlaceholderViewModel("Staff Reports & Analytics", "Department enrollment and passing rate reports are scheduled for Phase 2.")));
            AddNavItem("Profile", "👤", () => _navigationService.NavigateShellTo<StaffProfileViewModel>());

            // Default to Staff Dashboard
            _navigationService.NavigateShellTo<StaffDashboardViewModel>();
        }
        else if (IsAdmin)
        {
            AddNavItem("Dashboard", "📊", () => _navigationService.NavigateShellTo<AdminDashboardViewModel>());
            AddNavItem("Students", "🎓", () => _navigationService.NavigateShellTo(new PlaceholderViewModel("Student Management", "Comprehensive student enrollment directory and record administration is scheduled for Phase 2.")));
            AddNavItem("Staff", "👥", () => _navigationService.NavigateShellTo(new PlaceholderViewModel("Staff & Mentor Management", "Faculty account provisioning and mentor assignment management will be active in Phase 2.")));
            AddNavItem("Courses", "📚", () => _navigationService.NavigateShellTo(new PlaceholderViewModel("NPTEL Course Catalog", "Global NPTEL curriculum and syllabus administration is scheduled for Phase 2.")));
            AddNavItem("Registrations", "📝", () => _navigationService.NavigateShellTo(new PlaceholderViewModel("Registration Administration", "Institution-wide registration tracking and exam fee workflows are scheduled for Phase 2.")));
            AddNavItem("Certificates", "📜", () => _navigationService.NavigateShellTo(new PlaceholderViewModel("Certificate Verification", "Storage verification, document viewing, and credit approvals will be active in Phase 2.")));
            AddNavItem("Reports", "📈", () => _navigationService.NavigateShellTo(new PlaceholderViewModel("Institutional Reports", "NAAC/NBA accreditation export reports (PDF/Excel) are scheduled for Phase 2.")));
            AddNavItem("Settings", "⚙", () => _navigationService.NavigateShellTo(new PlaceholderViewModel("System Settings", "Department configuration and system governance will be available in Phase 2.")));
            AddNavItem("Profile", "👤", () => _navigationService.NavigateShellTo<AdminProfileViewModel>());

            // Default to Admin Dashboard
            _navigationService.NavigateShellTo<AdminDashboardViewModel>();
        }

        if (NavigationItems.Count > 0)
        {
            NavigationItems[0].IsSelected = true;
            _selectedItem = NavigationItems[0];
        }
    }

    private void AddNavItem(string title, string icon, Action navigateAction)
    {
        var item = new NavigationItemModel
        {
            Title = title,
            Icon = icon,
            Command = new RelayCommand(navigateAction)
        };
        NavigationItems.Add(item);
    }

    private async Task ExecuteLogoutAsync()
    {
        try
        {
            await _apiClient.LogoutAsync();
        }
        catch
        {
            // Clear session regardless of network outcome
        }
        finally
        {
            _navigationService.Logout();
        }
    }
}
