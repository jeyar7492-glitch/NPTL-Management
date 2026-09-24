using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class SplashView : UserControl
{
    public SplashView()
    {
        InitializeComponent();
        Loaded += SplashView_Loaded;
    }

    private async void SplashView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SplashViewModel vm)
        {
            await vm.PerformHealthCheckAsync();
        }
    }
}
