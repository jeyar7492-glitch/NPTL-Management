using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class AdminDashboardView : UserControl
{
    public AdminDashboardView()
    {
        InitializeComponent();
        Loaded += AdminDashboardView_Loaded;
    }

    private async void AdminDashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminDashboardViewModel vm && vm.AdminData == null)
        {
            await vm.LoadAdminDataAsync();
        }
    }
}
