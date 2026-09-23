using System.Windows;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop;

public partial class App : Application
{
    private INavigationService? _navigationService;
    private IApiClient? _apiClient;
    private IAuthenticationSession? _session;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var apiSettings = ApiSettings.Instance;
        _session = AuthenticationSession.Instance;
        _apiClient = new ApiClient(_session, apiSettings);

        _navigationService = new NavigationService(ResolveViewModel, _session);

        // Start at the Splash screen
        _navigationService.NavigateTo<SplashViewModel>();

        var mainVm = new MainWindowViewModel(_navigationService);
        var mainWindow = new MainWindow
        {
            DataContext = mainVm
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private ViewModelBase ResolveViewModel(Type type)
    {
        if (type == typeof(SplashViewModel))
            return new SplashViewModel(_apiClient!, _navigationService!);

        if (type == typeof(LoginViewModel))
            return new LoginViewModel(_apiClient!, _navigationService!, _session!);

        if (type == typeof(MainViewModel))
            return new MainViewModel(_navigationService!, _session!, _apiClient!);

        if (type == typeof(StudentDashboardViewModel))
            return new StudentDashboardViewModel(_apiClient!, _navigationService!);

        if (type == typeof(StudentCoursesViewModel))
            return new StudentCoursesViewModel(_apiClient!, _navigationService!);

        if (type == typeof(StudentNotificationsViewModel))
            return new StudentNotificationsViewModel(_apiClient!, _navigationService!);

        if (type == typeof(StudentProfileViewModel))
            return new StudentProfileViewModel(_apiClient!);

        if (type == typeof(StaffDashboardViewModel))
            return new StaffDashboardViewModel(_apiClient!, _navigationService!);

        if (type == typeof(StaffStudentsViewModel))
            return new StaffStudentsViewModel(_apiClient!, _navigationService!);

        if (type == typeof(StaffReportsViewModel))
            return new StaffReportsViewModel(_apiClient!, _navigationService!);

        if (type == typeof(StaffProfileViewModel))
            return new StaffProfileViewModel(_apiClient!);

        if (type == typeof(AdminDashboardViewModel))
            return new AdminDashboardViewModel(_apiClient!);

        if (type == typeof(AdminProfileViewModel))
            return new AdminProfileViewModel(_apiClient!);

        if (type == typeof(PlaceholderViewModel))
            return new PlaceholderViewModel();

        throw new InvalidOperationException($"No ViewModel registration found for {type.FullName}");
    }
}
