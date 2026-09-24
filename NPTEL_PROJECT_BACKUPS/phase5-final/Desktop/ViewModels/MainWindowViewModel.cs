using NPTELManagement.Desktop.Common;

namespace NPTELManagement.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ViewModelBase? CurrentView => _navigationService.CurrentView;

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.NavigationChanged += () =>
        {
            OnPropertyChanged(nameof(CurrentView));
        };
    }
}
