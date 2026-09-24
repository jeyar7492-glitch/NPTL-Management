using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class StaffDashboardView : UserControl
{
    public StaffDashboardView()
    {
        InitializeComponent();
        Loaded += StaffDashboardView_Loaded;
    }

    private async void StaffDashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StaffDashboardViewModel vm && vm.StaffProfile == null)
        {
            await vm.LoadDataAsync();
        }
    }
}
