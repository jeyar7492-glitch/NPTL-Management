using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Common;

public interface INavigationService
{
    ViewModelBase? CurrentView { get; }
    ViewModelBase? CurrentShellView { get; }

    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    void NavigateTo(ViewModelBase viewModel);

    void NavigateShellTo<TViewModel>() where TViewModel : ViewModelBase;
    void NavigateShellTo(ViewModelBase viewModel);

    void Logout();

    event Action? NavigationChanged;
    event Action? ShellNavigationChanged;
}
