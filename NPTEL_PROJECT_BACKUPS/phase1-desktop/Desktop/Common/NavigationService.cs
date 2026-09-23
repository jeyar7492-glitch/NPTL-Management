using NPTELManagement.Desktop.Services;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Common;

public class NavigationService : INavigationService
{
    private readonly Func<Type, ViewModelBase> _viewModelFactory;
    private readonly IAuthenticationSession _session;

    private ViewModelBase? _currentView;
    private ViewModelBase? _currentShellView;

    public ViewModelBase? CurrentView
    {
        get => _currentView;
        private set
        {
            _currentView = value;
            NavigationChanged?.Invoke();
        }
    }

    public ViewModelBase? CurrentShellView
    {
        get => _currentShellView;
        private set
        {
            _currentShellView = value;
            ShellNavigationChanged?.Invoke();
        }
    }

    public event Action? NavigationChanged;
    public event Action? ShellNavigationChanged;

    public NavigationService(Func<Type, ViewModelBase> viewModelFactory, IAuthenticationSession? session = null)
    {
        _viewModelFactory = viewModelFactory;
        _session = session ?? AuthenticationSession.Instance;
        _session.SessionExpired += OnSessionExpired;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var vm = _viewModelFactory(typeof(TViewModel));
        NavigateTo(vm);
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
    }

    public void NavigateShellTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var vm = _viewModelFactory(typeof(TViewModel));
        NavigateShellTo(vm);
    }

    public void NavigateShellTo(ViewModelBase viewModel)
    {
        CurrentShellView = viewModel;
    }

    public void Logout()
    {
        _session.ClearSession();
        _currentShellView = null;
        NavigateTo<LoginViewModel>();
    }

    private void OnSessionExpired()
    {
        // When session expires or token becomes invalid, force return to login
        _currentShellView = null;
        NavigateTo<LoginViewModel>();
    }
}
